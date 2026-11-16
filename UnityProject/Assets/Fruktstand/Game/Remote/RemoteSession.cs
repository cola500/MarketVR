// The Quest's side of the phone-customer protocol (EPIC-004), without any networking: text in, text out, so it is
// tested directly. RelayLink feeds it the relay's messages on the main thread and sends whatever it returns.
//
// Accepted from the relay (everything else, e.g. "pay" or a "wallet" field, is ignored):
//   {"t":"order","orderId":"…","items":["apple"],"photoId":"…"}   -> RemoteCustomer.Request (the Quest's rules decide)
//   {"t":"photo","photoId":"…","w":128,"h":128,"rgba":"<base64>"} -> PhotoStore (validated, decoded once per id)
//   {"t":"sync","orderId":"…"}                                    -> the current status of that order
// Sent to the relay (for the phones): {"t":"status","orderId":…,"state":…,"reason":…,"sum":…,"duplicate":…} and
// {"t":"hello","role":"quest",…} on connect. Status messages never carry image data.
// EPIC-008, a separate parent message (not a customer one): {"t":"parent","cmd":"rest"|"resume"|"nextRound","cmdId":"…"}.
// Only these two commands; each command id acts at most once (a resent one is only acknowledged). The game decides
// (OnParentCommand) and the reply {"t":"parentAck",…} only says it was received; the phone shows "paused" only from
// the market status {"t":"market","state":"playing"|"party"|"resting","fruit":["pear",…]} that follows the actual
// change (EPIC-009: also sent when the fruit on sale change; an order of other fruit is rejected "inactive").
using System;
using System.Collections.Generic;
using Fruktstand.Core;
using UnityEngine;

namespace Fruktstand.Game
{
    public sealed class RemoteSession
    {
        public const int MaxMessageChars = 96 * 1024; // the largest message is one 128 x 128 photo (~87 k chars)

        [Serializable]
        sealed class Incoming
        {
            public string t;
            public string orderId;
            public string photoId;
            public string[] items;
            public int w, h;
            public string rgba;
            public string cmd, cmdId;
        }

        [Serializable]
        sealed class ParentAck
        {
            public string t = "parentAck";
            public string cmdId;
            public string cmd;
            public bool accepted;
            public bool duplicate;
        }

        [Serializable]
        sealed class MarketStatus
        {
            public string t = "market";
            public string state;
            public string[] fruit; // EPIC-009: the three fruit/veg on sale now (phone keys, bin order)
            public string[] candy; // EPIC-010: the three candy on sale now (toys can't be ordered from the phone)
            public string restBy;  // EPIC-012: "child" | "parent" | "party" while resting, "" otherwise
        }

        // The game's answer to a parent command: true if it will do it (a rest now / the next round from a rest).
        public Func<string, bool> OnParentCommand { get; set; }
        readonly Dictionary<string, ParentAck> parentDone = new();
        readonly Queue<string> parentOrder = new();
        int sentMarket = -1, sentAssortment = -1;

        [Serializable]
        sealed class Status
        {
            public string t = "status";
            public string orderId;
            public string state;
            public string reason;
            public int sum;
            public bool duplicate;
        }

        [Serializable]
        sealed class Hello
        {
            public string t = "hello";
            public string role = "quest";
            public string build;
            public string[] photos; // photo ids the Quest already has (the relay resends only the missing)
        }

        readonly RemoteCustomer remote;
        readonly PhotoStore photos;
        readonly Shop shop;
        int sentRevision = -1;
        string lastActiveId;

        public RemoteSession(Shop shop, PhotoStore photos)
        {
            this.shop = shop;
            remote = shop.Remote;
            this.photos = photos;
        }

        public int Ignored { get; private set; } // messages that were not understood or not allowed

        public static string StateWord(RemoteState s) => s switch
        {
            RemoteState.Waiting => "waiting",
            RemoteState.Serving => "serving",
            RemoteState.Done => "done",
            RemoteState.Cancelled => "cancelled",
            RemoteState.Rejected => "rejected",
            _ => "unknown",
        };

        public string HelloMessage() => JsonUtility.ToJson(new Hello { build = BuildInfo.Id, photos = photos.Ids() });

        // One message from the relay. Returns the replies (status) to send back; never throws.
        public List<string> Handle(string json)
        {
            var replies = new List<string>();
            Incoming m = null;
            if (json != null && json.Length <= MaxMessageChars)
            {
                try { m = JsonUtility.FromJson<Incoming>(json); }
                catch (Exception) { m = null; }
            }
            switch (m?.t)
            {
                case "order":
                    var receipt = remote.Request(m.orderId, m.items, m.photoId);
                    Debug.Log($"[Fruktstand] phone order {Short(m.orderId)}: {StateWord(receipt.State)}{(receipt.Duplicate ? " (duplicate)" : "")} {receipt.Reason}");
                    replies.Add(StatusJson(receipt));
                    break;
                case "sync":
                    replies.Add(StatusJson(remote.Status(m.orderId)));
                    break;
                case "parent":
                    replies.Add(Parent(m.cmd, m.cmdId));
                    break;
                case "photo":
                    var ok = photos.TryAdd(m.photoId, m.w, m.h, m.rgba);
                    Debug.Log($"[Fruktstand] phone photo {Short(m.photoId)}: {(ok ? "ok" : "rejected")}");
                    if (!ok) Ignored++;
                    break;
                default:
                    Ignored++;
                    break;
            }
            return replies;
        }

        // Called every frame: a status message whenever the active order's (or the last finished one's) state changed.
        public string Pump()
        {
            if (remote.Revision == sentRevision) return null;
            sentRevision = remote.Revision;
            if (remote.ActiveId != null) lastActiveId = remote.ActiveId;
            return lastActiveId != null ? StatusJson(remote.Status(lastActiveId)) : null;
        }

        string Parent(string cmd, string cmdId)
        {
            if (cmd is not ("rest" or "resume" or "nextRound") || !RemoteCustomer.ValidId(cmdId))
            {
                Ignored++;
                return JsonUtility.ToJson(new ParentAck { cmdId = cmdId ?? "", cmd = cmd ?? "", accepted = false });
            }
            if (parentDone.TryGetValue(cmdId, out var known))
                return JsonUtility.ToJson(new ParentAck { cmdId = cmdId, cmd = known.cmd, accepted = known.accepted, duplicate = true });
            var accepted = OnParentCommand?.Invoke(cmd) ?? false;
            var ack = new ParentAck { cmdId = cmdId, cmd = cmd, accepted = accepted };
            parentDone[cmdId] = ack;
            parentOrder.Enqueue(cmdId);
            while (parentOrder.Count > 64) parentDone.Remove(parentOrder.Dequeue());
            Debug.Log($"[Fruktstand] parent command from the phone: {cmd} ({(accepted ? "accepted" : "not now")})");
            return JsonUtility.ToJson(ack);
        }

        // Called every frame: the market's state whenever it changed (the phone shows "paused" from this).
        public string PumpMarket()
        {
            var round = shop.Round;
            if (round.Revision == sentMarket && shop.Assortment.Revision == sentAssortment) return null;
            sentMarket = round.Revision;
            sentAssortment = shop.Assortment.Revision;
            return JsonUtility.ToJson(new MarketStatus { state = MarketWord(round.State), fruit = Keys(Category.FruitVeg), candy = Keys(Category.Candy), restBy = RestWord(round.RestBy) });
        }

        string[] Keys(Category c)
        {
            var wares = shop.Assortment.Active(c);
            var keys = new string[wares.Length];
            for (var i = 0; i < wares.Length; i++) keys[i] = RemoteCustomer.KeyOf(wares[i]);
            return keys;
        }

        public void ResendMarket() => sentMarket = -1; // after a reconnect

        public static string RestWord(RestCause c) => c switch
        {
            RestCause.Child => "child",
            RestCause.Parent => "parent",
            RestCause.Party => "party",
            _ => "",
        };

        public static string MarketWord(RoundState s) => s switch
        {
            RoundState.Resting => "resting",
            RoundState.PartyPending or RoundState.Celebrating => "party",
            _ => "playing",
        };

        static string StatusJson(RemoteReceipt r) => JsonUtility.ToJson(new Status
        {
            orderId = r.OrderId ?? "",
            state = StateWord(r.State),
            reason = r.Reason,
            sum = r.Sum,
            duplicate = r.Duplicate,
        });

        static string Short(string id) => id == null ? "-" : id.Length > 8 ? id.Substring(0, 8) : id;

        // Exposed for the view: the human customer of the current turn and their photo (or null: default figure).
        public Texture2D PhotoForCurrentCustomer => shop.CustomerIsHuman ? photos.Get(shop.HumanPhotoId) : null;
    }
}
