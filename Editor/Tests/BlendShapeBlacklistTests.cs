using NUnit.Framework;

namespace TY.FXFacialAssistant.Tests
{
    public sealed class BlendShapeBlacklistTests
    {
        [TestCase("vrc.Blink")]
        [TestCase("VRC.V_AA")]
        [TestCase("vrm.BLINK_L")]
        [TestCase("VRM_BLINK_R")]
        [TestCase("eyeBlinkLeft")]
        [TestCase("EyeBlinkLeft")]
        [TestCase("EYEBLINKLEFT")]
        [TestCase("EyeClosedLeft")]
        [TestCase("lipSuckUpperRight")]
        [TestCase("NeckFlexRight")]
        [TestCase("MouthSmile")]
        [TestCase("Eye_Left_Blink")]
        [TestCase("MOUTH_APE_SHAPE")]
        [TestCase("mouthFunnelLeft")]
        [TestCase("ex_目幅広げ")]
        [TestCase("EX_Custom")]
        public void DefaultRulesMatchRequestedStandardsIgnoringCase(string name)
            => Assert.That(new BlendShapeBlacklist(FacialBlacklistDefaults.Rules).Matches(name), Is.True);

        [TestCase("笑い")]
        [TestCase("まばたき")]
        [TestCase("CustomEyeBlinkLeft")]
        [TestCase("EyeBlinkLeft_Custom")]
        [TestCase("vrcCustom")]
        public void DefaultRulesDoNotUseBroadTrackingNamePrefixes(string name)
            => Assert.That(new BlendShapeBlacklist(FacialBlacklistDefaults.Rules).Matches(name), Is.False);

        [TestCase("Prefix*", "PREFIXSmile", true)]
        [TestCase("*Smile", "HappySMILE", true)]
        [TestCase("Eye?Left", "eyexleft", true)]
        [TestCase("Eye?Left", "EyeLeft", false)]
        [TestCase("*Eye*Left", "CustomEyeUpperLeft", true)]
        [TestCase("*Eye*Left", "CustomEyeUpperRight", false)]
        [TestCase("*", "*Custom", true)]
        [TestCase("Shape.L", "ShapeXL", false)]
        public void CustomRulesSupportFullNameWildcards(string rule, string name, bool expected)
            => Assert.That(new BlendShapeBlacklist(rule).Matches(name), Is.EqualTo(expected));

        [Test]
        public void CommentsWhitespaceAndDuplicateCaseVariantsAreIgnored()
        {
            var blacklist = new BlendShapeBlacklist(" # comment\r\n EyeBlinkLeft \n EYEBLINKLEFT\n\nvrc.*\nVRC.*");
            Assert.That(blacklist.RuleCount, Is.EqualTo(2));
            Assert.That(blacklist.Matches("EyeBlinkLeft"), Is.True);
            Assert.That(blacklist.Matches("VRC.Blink"), Is.True);
            Assert.That(blacklist.Matches("# comment"), Is.False);
        }

        [Test]
        public void EmptyRulesDisableBlacklist()
        {
            var blacklist = new BlendShapeBlacklist("");
            Assert.That(blacklist.RuleCount, Is.Zero);
            Assert.That(blacklist.Matches("vrc.Blink"), Is.False);
        }

        [Test]
        public void SettingsRoundTripPreservesModeAndCustomRules()
        {
            var settings = new FacialBlacklistSettings { Display = BlacklistedShapeDisplay.Hide, Rules = "custom*\nEXACT" };
            var restored = UnityEngine.JsonUtility.FromJson<FacialBlacklistSettings>(UnityEngine.JsonUtility.ToJson(settings));
            Assert.That(restored.Display, Is.EqualTo(BlacklistedShapeDisplay.Hide));
            Assert.That(restored.Rules, Is.EqualTo(settings.Rules));
            Assert.That(restored.Signature, Is.EqualTo(settings.Signature));
        }
    }
}
