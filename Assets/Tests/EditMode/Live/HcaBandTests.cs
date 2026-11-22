using System;
using System.Linq;
using AcbDecoder;
using NUnit.Framework;

namespace OpenSekai.Tests.EditMode
{
    public sealed class HcaBandTests
    {
        // Two CRC-checked, one-frame mono HCA v3 streams. Band 125 has a different
        // positive coefficient in each subframe. The first stream reconstructs band
        // 126 at unity gain using HFR. The second codes the same coefficient explicitly.
        private const string RECONSTRUCTED = "SENBAAMAACpmbXQAAQC7gAAAAAEAAAAAY29tcABzAQ8BAIB+AAEAACc4//8AAMAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAB54APsPwP0P4P8QAQEQIASqg==";
        private const string EXPLICIT = "SENBAAMAACpmbXQAAQC7gAAAAAEAAAAAY29tcAB/AQ8BAICAAAAAAPB8//8AAMAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAB54APsPsPwPwP0P0P4P4P8P8QAQAQEQEQIQIAI6g==";

        [Test]
        public void All128ScaleFactorsProduceTheExplicitlyCodedWaveform()
        {
            var hfr = Convert.FromBase64String(RECONSTRUCTED);
            var direct = Convert.FromBase64String(EXPLICIT);
            float[] expected = HcaDecoder.Decode(direct, 0, direct.Length).Samples;
            float[] actual = HcaDecoder.Decode(hfr, 0, hfr.Length).Samples;
            Assert.That(expected.Length, Is.EqualTo(1024));
            Assert.That(expected.Any(sample => Math.Abs(sample) > 0.0001f), Is.True);
            Assert.That(actual, Is.EqualTo(expected));
        }
    }
}
