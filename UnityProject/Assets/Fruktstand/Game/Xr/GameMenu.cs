// The in-app menu (controller Menu button; the Meta/system button is never touched). Adapted from Magic Tiles.
// Pure state machine so it is unit-tested; MenuPanel only draws Rows().
// "Töm korgen" only restarts the current order; "Nytt spel" is the only thing that empties money and candy and
// needs a deliberate Ja (Nej is preselected).
// "Rikta in marknaden" (EPIC-007) opens the calibration page: the stick up/down picks a row, left/right changes a
// value (Adjust), A/trigger acts, B cancels. The caller runs the Calibration and closes the menu on a confirmation.
// EPIC-012: every page is a list of rows (title, text, buttons). Buttons are numbered 0..n per page; the stick and A
// pick and press the selected one, a pointer (MenuPanel ray) picks one with SelectItem. The items follow the pause:
// a child's rest is ended here ("Fortsätt spela"), a parent's pause and the rest after the party only by the parent.
using System.Collections.Generic;
using Fruktstand.Core;
using UnityEngine;

namespace Fruktstand.Game
{
    public enum MenuAction
    {
        None, Resume, ToggleOpen, ClearBasket, TidyPlay, PlaceInFront, NewGame,
        Calibrate, CalibConfirm, CalibCancel, CalibWidth, CalibLength, CalibRotate, CalibHere, CalibFace, CalibUseSystem,
        TakeRest, NextRound, // EPIC-008
        ResumeRest, ParentRest, ParentResume, // EPIC-012
        CycleNatureSound, // EPIC-014
    }

    public enum RowKind { Title, Text, Button }
    public enum RowStyle { Normal, Go, Danger, Muted }

    public readonly struct MenuRow
    {
        public MenuRow(RowKind kind, string text, int item = -1, bool selected = false, RowStyle style = RowStyle.Normal)
        {
            Kind = kind;
            Text = text;
            Item = item;
            Selected = selected;
            Style = style;
        }

        public RowKind Kind { get; }
        public string Text { get; }
        public int Item { get; }       // button number on its page, -1 for text
        public bool Selected { get; }
        public RowStyle Style { get; }
    }

    public sealed class GameMenu
    {
        enum Page { Closed, Main, Controls, ConfirmNewGame, Calibrate, Parent, ConfirmNextRound }

        // One button: what it says, how it looks and what pressing it does.
        readonly struct Entry
        {
            public Entry(string label, System.Func<MenuAction> act, RowStyle style = RowStyle.Normal)
            {
                Label = label;
                Act = act;
                Style = style;
            }

            public string Label { get; }
            public System.Func<MenuAction> Act { get; }
            public RowStyle Style { get; }
        }

        public const string ControlsText =
            "Håll handen vid en sak: den lyser.\n" +
            "Grepp eller avtryckare: ta.\n" +
            "Släpp vid korgen: lägg i.\n" +
            "Brickan i kassan: ta betalt.\n" +
            "Vänster spak: gå.\n" +
            "Höger spak vänster/höger: vänd dig 30°.\n" +
            "Höger spak upp/ned: höj/sänk diskarna.\n" +
            "Dra handen genom Öppet/Stängt: pausa kunder.\n" +
            "Menyknappen: öppna och stäng menyn.\n" +
            "I menyn: peka med strålen och tryck på avtryckaren.\n" +
            "Eller: spak upp/ned och A. B går tillbaka.";

        public const int ControlsPerPage = 6;
        static int ControlsPages => (ControlsText.Split('\n').Length + ControlsPerPage - 1) / ControlsPerPage;

        Page page = Page.Closed;
        int selected;
        int controlsPage;

        // Shown on the toggle item; set by the caller every frame.
        public bool StandOpen { get; set; } = true;
        public bool IsOpen => page != Page.Closed;
        public bool ShowingControls => page == Page.Controls;
        public int ControlsPageIndex => controlsPage;
        public bool Confirming => page == Page.ConfirmNewGame;
        public int Selected => selected;
        public bool Calibrating => page == Page.Calibrate;
        public Calibration Calibration { get; set; }    // EPIC-007; lines and rows of the calibration page
        public int AdjustDirection { get; private set; } // -1 / +1 for the last Adjust
        public MenuAction LastAction { get; private set; }
        public IReadOnlyList<string> ParentLines { get; set; } = new string[0]; // the round's numbers for the parent
        public bool ShowingParent => page == Page.Parent;
        public bool ConfirmingNextRound => page == Page.ConfirmNextRound;

        // EPIC-012: why the market rests (set by the caller every frame). EPIC-008's Resting is "any rest".
        public RestCause RestBy { get; set; }
        public Core.NatureSound NatureSound { get; set; } // EPIC-014 (set by the caller every frame)

        static string NatureSoundName(Core.NatureSound s) => s switch { Core.NatureSound.Normal => "normalt", Core.NatureSound.Low => "svagt", _ => "av" };
        public bool Resting
        {
            get => RestBy != RestCause.None;
            set => RestBy = value ? (RestBy == RestCause.None ? RestCause.Party : RestBy) : RestCause.None;
        }

        // Last line of the main page, so the installed build can be identified (BuildInfo; settable for tests).
        // Read lazily: GameMenu is created in field initialisers, where Resources.Load is not allowed.
        string buildLine;
        public string BuildLine
        {
            get => buildLine ??= BuildInfo.MenuLine;
            set => buildLine = value;
        }

        IReadOnlyList<CalibItem> CalibItems => Calibration?.Items ?? new[] { CalibItem.Confirm, CalibItem.Cancel };

        // ---- pages ----

        List<Entry> Entries()
        {
            var e = new List<Entry>();
            switch (page)
            {
                case Page.Main:
                    e.Add(RestBy switch
                    {
                        RestCause.Child => new Entry("Fortsätt spela", () => CloseWith(MenuAction.ResumeRest), RowStyle.Go),
                        RestCause.None => new Entry("Fortsätt", () => CloseWith(MenuAction.Resume), RowStyle.Go),
                        _ => new Entry("Stäng menyn", () => CloseWith(MenuAction.Resume)),
                    });
                    e.Add(new Entry("Visa kontroller", () => Goto(Page.Controls)));
                    e.Add(new Entry(StandOpen ? "Stäng marknaden" : "Öppna marknaden", () => CloseWith(MenuAction.ToggleOpen)));
                    e.Add(new Entry("Återställ beställningen", () => CloseWith(MenuAction.ClearBasket)));
                    e.Add(new Entry("Städa lekytan", () => CloseWith(MenuAction.TidyPlay)));
                    e.Add(new Entry("Flytta marknaden hit", () => CloseWith(MenuAction.PlaceInFront)));
                    e.Add(new Entry("Rikta in marknaden", () =>
                    {
                        OpenCalibration();
                        return MenuAction.Calibrate;
                    }));
                    if (RestBy == RestCause.None) e.Add(new Entry("Ta vilopaus", () => CloseWith(MenuAction.TakeRest)));
                    e.Add(new Entry("För föräldrar", () => Goto(Page.Parent)));
                    e.Add(new Entry("Nytt spel", () => Goto(Page.ConfirmNewGame), RowStyle.Danger));
                    break;
                case Page.Controls:
                    if (controlsPage < ControlsPages - 1) e.Add(new Entry("Nästa sida", () =>
                    {
                        controlsPage++;
                        selected = 0;
                        return MenuAction.None;
                    }));
                    e.Add(new Entry("Tillbaka", BackInner));
                    break;
                case Page.Parent:
                    if (RestBy == RestCause.None) e.Add(new Entry("Pausa spelet", () => CloseWith(MenuAction.ParentRest)));
                    if (RestBy is RestCause.Child or RestCause.Parent) e.Add(new Entry("Fortsätt spelet", () => CloseWith(MenuAction.ParentResume), RowStyle.Go));
                    e.Add(new Entry($"Naturljud: {NatureSoundName(NatureSound)}", () => MenuAction.CycleNatureSound)); // EPIC-014: stays open
                    e.Add(Resting
                        ? new Entry("Starta nästa omgång", () => Goto(Page.ConfirmNextRound))
                        : new Entry("Starta nästa omgång (bara i vilopaus)", () => MenuAction.None, RowStyle.Muted));
                    e.Add(new Entry("Tillbaka", BackInner));
                    break;
                case Page.ConfirmNextRound:
                    e.Add(new Entry("Nej, vila lite till", BackInner, RowStyle.Go));
                    e.Add(new Entry("Ja, starta nästa omgång", () => CloseWith(MenuAction.NextRound)));
                    break;
                case Page.ConfirmNewGame:
                    e.Add(new Entry("Nej, fortsätt spela", BackInner, RowStyle.Go));
                    e.Add(new Entry("Ja, nytt spel", () => CloseWith(MenuAction.NewGame), RowStyle.Danger));
                    break;
                case Page.Calibrate:
                    foreach (var item in CalibItems)
                    {
                        var it = item;
                        e.Add(new Entry(CalibLabel(it), () => CalibSelect(it), it == CalibItem.Cancel ? RowStyle.Normal : it == CalibItem.Confirm ? RowStyle.Go : RowStyle.Normal));
                    }
                    break;
            }
            return e;
        }

        // Text above the buttons.
        IEnumerable<MenuRow> Header()
        {
            switch (page)
            {
                case Page.Main:
                    yield return new MenuRow(RowKind.Title, "Meny");
                    if (RestBy == RestCause.Child) yield return new MenuRow(RowKind.Text, "Du vilar. Fortsätt spela när du vill.");
                    if (RestBy == RestCause.Parent) yield return new MenuRow(RowKind.Text, "Föräldern har pausat spelet. En vuxen fortsätter det.");
                    if (RestBy == RestCause.Party) yield return new MenuRow(RowKind.Text, "Pausdags! En vuxen startar nästa omgång.");
                    break;
                case Page.Controls:
                    yield return new MenuRow(RowKind.Title, ControlsPages > 1 ? $"Kontroller {controlsPage + 1}/{ControlsPages}" : "Kontroller");
                    var lines = ControlsText.Split('\n');
                    for (var i = controlsPage * ControlsPerPage; i < Mathf.Min(lines.Length, (controlsPage + 1) * ControlsPerPage); i++)
                        yield return new MenuRow(RowKind.Text, lines[i]);
                    break;
                case Page.Calibrate:
                    yield return new MenuRow(RowKind.Title, "Rikta in marknaden");
                    if (Calibration != null)
                        foreach (var line in Calibration.StatusLines()) yield return new MenuRow(RowKind.Text, line);
                    break;
                case Page.Parent:
                    yield return new MenuRow(RowKind.Title, "För föräldrar");
                    foreach (var line in ParentLines) yield return new MenuRow(RowKind.Text, line);
                    break;
                case Page.ConfirmNextRound:
                    yield return new MenuRow(RowKind.Title, "Starta nästa omgång?");
                    yield return new MenuRow(RowKind.Text, "Kassan och stjärnorna finns kvar.");
                    break;
                case Page.ConfirmNewGame:
                    yield return new MenuRow(RowKind.Title, "Nytt spel?");
                    yield return new MenuRow(RowKind.Text, "Kassan töms och allt börjar om.");
                    break;
            }
        }

        // Text under the buttons.
        IEnumerable<MenuRow> Footer()
        {
            if (page == Page.Calibrate) yield return new MenuRow(RowKind.Text, "Spak vänster/höger: ändra värdet");
            if (page == Page.Main && Calibration?.SuggestRecalibration == true) yield return new MenuRow(RowKind.Text, "Spelytan har ändrats: välj Rikta in marknaden");
            if (page == Page.Main && !string.IsNullOrEmpty(BuildLine)) yield return new MenuRow(RowKind.Text, BuildLine, style: RowStyle.Muted);
        }

        // ---- the page as rows, for the panel and the pointer ----

        public IReadOnlyList<MenuRow> Rows()
        {
            var rows = new List<MenuRow>();
            if (page == Page.Closed) return rows;
            rows.AddRange(Header());
            var entries = Entries();
            for (var i = 0; i < entries.Count; i++) rows.Add(new MenuRow(RowKind.Button, entries[i].Label, i, i == selected, entries[i].Style));
            rows.AddRange(Footer());
            return rows;
        }

        public int ItemCount => page == Page.Closed ? 0 : Entries().Count;

        // The plain-text page (logs and tests): buttons as "> chosen" / "   other".
        public IEnumerable<string> Lines()
        {
            if (page == Page.Closed) yield break;
            var buttonsStarted = false;
            foreach (var r in Rows())
            {
                if (r.Kind == RowKind.Button && !buttonsStarted)
                {
                    buttonsStarted = true;
                    yield return "";
                }
                yield return r.Kind == RowKind.Button ? (r.Selected ? "> " : "   ") + r.Text : r.Text;
            }
            yield return "";
            yield return "Peka och tryck på avtryckaren";
            yield return "Spak + A: välj   B: tillbaka";
        }

        // ---- input ----

        public void OpenCalibration()
        {
            page = Page.Calibrate;
            selected = 0;
        }

        public void OpenParent() => Goto(Page.Parent);

        // Stick left/right on the calibration page: changes the selected value row.
        public MenuAction Adjust(int dir)
        {
            if (page != Page.Calibrate || dir == 0) return MenuAction.None;
            AdjustDirection = dir > 0 ? 1 : -1;
            return LastAction = CalibItems[selected] switch
            {
                CalibItem.Width => MenuAction.CalibWidth,
                CalibItem.Length => MenuAction.CalibLength,
                CalibItem.Rotate => MenuAction.CalibRotate,
                _ => MenuAction.None,
            };
        }

        public void Open()
        {
            page = Page.Main;
            selected = 0;
        }

        public void Close() => page = Page.Closed;

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Navigate(int step)
        {
            var count = ItemCount;
            if (count > 0) selected = (selected + step % count + count) % count;
        }

        // EPIC-012: the pointer rests on a button: it becomes the selected one.
        public void Hover(int item)
        {
            if (item >= 0 && item < ItemCount) selected = item;
        }

        // EPIC-012: a pointer click on a button: that button, exactly as A on it would.
        public MenuAction SelectItem(int item)
        {
            if (item < 0 || item >= ItemCount) return LastAction = MenuAction.None;
            selected = item;
            return Select();
        }

        public MenuAction Select()
        {
            var entries = Entries();
            if (page == Page.Closed || entries.Count == 0) return LastAction = MenuAction.None;
            return LastAction = entries[Mathf.Clamp(selected, 0, entries.Count - 1)].Act();
        }

        public MenuAction Back() => LastAction = BackInner();

        MenuAction BackInner()
        {
            switch (page)
            {
                case Page.Calibrate:
                    Close();
                    return MenuAction.CalibCancel;
                case Page.ConfirmNextRound:
                    Goto(Page.Parent);
                    selected = IndexOf("Starta nästa omgång");
                    return MenuAction.None;
                case Page.Parent:
                    Goto(Page.Main);
                    selected = IndexOf("För föräldrar");
                    return MenuAction.None;
                case Page.Main:
                    Close();
                    return MenuAction.None;
                case Page.Controls:
                    Goto(Page.Main);
                    selected = IndexOf("Visa kontroller");
                    return MenuAction.None;
                case Page.ConfirmNewGame:
                    Goto(Page.Main);
                    selected = IndexOf("Nytt spel");
                    return MenuAction.None;
                default:
                    return MenuAction.None;
            }
        }

        MenuAction CloseWith(MenuAction action)
        {
            Close();
            return action;
        }

        MenuAction Goto(Page p)
        {
            page = p;
            selected = 0; // on a confirmation the first button is "Nej": a reset needs a deliberate second choice
            if (p == Page.Controls) controlsPage = 0;
            return MenuAction.None;
        }

        int IndexOf(string label)
        {
            var e = Entries();
            for (var i = 0; i < e.Count; i++) if (e[i].Label == label) return i;
            return 0;
        }

        MenuAction CalibSelect(CalibItem item)
        {
            switch (item)
            {
                case CalibItem.Confirm: return MenuAction.CalibConfirm; // the caller closes it if it went through
                case CalibItem.Here: return MenuAction.CalibHere;
                case CalibItem.Face: return MenuAction.CalibFace;
                case CalibItem.UseSystem: return MenuAction.CalibUseSystem;
                case CalibItem.Cancel:
                    Close();
                    return MenuAction.CalibCancel;
                default: return MenuAction.None; // value rows change with the stick
            }
        }

        string CalibLabel(CalibItem item)
        {
            var c = Calibration;
            return item switch
            {
                CalibItem.Confirm => c != null && !c.Fit.Fits ? "Bekräfta (får inte plats)" : "Bekräfta",
                CalibItem.Width => $"Bredd: {Calibration.Sv(c?.Size.x ?? 0)} m",
                CalibItem.Length => $"Längd: {Calibration.Sv(c?.Size.y ?? 0)} m",
                CalibItem.Rotate => $"Vrid: {Mathf.RoundToInt(c?.Yaw ?? 0)}°",
                CalibItem.Here => "Mitten där jag står",
                CalibItem.Face => "Vrid dit jag tittar",
                CalibItem.UseSystem => "Använd systemets spelyta",
                _ => "Avbryt",
            };
        }
    }
}
