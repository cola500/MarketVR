// The app's name (MarketVR, formerly Lilla fruktståndet): the build configures what the project says, and the
// Android application id stays the same so that a new APK updates the installed app.
using Fruktstand.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;

namespace Fruktstand.Tests
{
    public class AppNameTests
    {
        [Test]
        public void App_NamedMarketVR()
        {
            Assert.AreEqual("MarketVR", PlayerSettings.productName, "the project's name");
            Assert.AreEqual(PlayerSettings.productName, QuestBuild.ProductName, "the build sets the same name (it overwrote it before)");
        }

        [Test]
        public void App_IdUnchangedSoTheQuestUpdates()
        {
            Assert.AreEqual("com.lillafruktstandet.prototype", QuestBuild.ApplicationId);
            Assert.AreEqual(QuestBuild.ApplicationId, PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android));
        }
    }
}
