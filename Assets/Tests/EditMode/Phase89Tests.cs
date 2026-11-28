using CQRa.Sim.Core;
using NUnit.Framework;

namespace CQRa.Tests.EditMode
{
    public sealed class Phase89Tests
    {
        [Test]
        public void SaveSlotDescriptions_AreLabelled()
        {
            for (var slot = 0; slot < SaveSlots.Count; slot++)
            {
                var text = SaveSlots.Describe(slot);
                StringAssert.StartsWith($"SLOT {slot + 1}:", text);
            }
        }
    }
}
