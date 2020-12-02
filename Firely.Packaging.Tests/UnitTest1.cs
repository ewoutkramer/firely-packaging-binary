using Microsoft.VisualStudio.TestTools.UnitTesting;
using Hl7.Fhir.Specification.Source;
using System.Threading.Tasks;
using System.Dynamic;
using T=Hl7.Fhir.ElementModel.Types;
using Firely.Packaging.Binary.MessagePack;
using System.Buffers;
using MessagePack;
using System.Collections.Generic;

namespace Firely.Packaging.Tests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public async Task TestMethod1()
        {
            var spec = ZipSource.CreateValidationSource();

            var sd = await spec.FindStructureDefinitionForCoreTypeAsync("Patient");
            Assert.IsNotNull(sd);
        }

        [TestMethod]
        public void RoundtripSimpleExpando()
        {
            var expando = new ExpandoObject();
            dynamic source = expando;
            var birthDate = T.Date.Parse("1972-11-30");

            source.name = "Ewout";
            source.active = true;
            source.nested = new ExpandoObject();
            source.nested.data = "Wednesday";
            source.identifier = new List<object> { 104231m, 2 };
            source.birthDate = birthDate;

            dynamic contact1 = new ExpandoObject();
            contact1.name = "Marleen";
            source.contact = new List<dynamic> { contact1 };

            var memory = PrimitiveObjectFormatterCore.SerializeToMemory((object)source);
            var reader = new MessagePackReader(memory);
            dynamic parsed = PrimitiveObjectFormatterCore.Deserialize(ref reader);

            Assert.AreEqual("Ewout", parsed.name);
            Assert.AreEqual("Marleen", parsed.contact[0].name);
            Assert.AreEqual("Wednesday", source.nested.data);
            Assert.AreEqual(104231m, parsed.identifier[0]);
            Assert.AreEqual(2, parsed.identifier[1]);
            Assert.AreEqual(birthDate, parsed.birthDate);

            Assert.IsFalse(((IDictionary<string,object>)expando).ContainsKey("doesnotexist"));
            Assert.IsTrue(((IDictionary<string, object>)expando).ContainsKey("name"));
        }
    }
}
