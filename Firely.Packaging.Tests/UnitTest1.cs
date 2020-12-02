using Microsoft.VisualStudio.TestTools.UnitTesting;
using Hl7.Fhir.Specification.Source;
using System.Threading.Tasks;
using System.Dynamic;
using T = Hl7.Fhir.ElementModel.Types;
using Firely.Packaging.Binary.MessagePack;
using System.Collections.Generic;
using Hl7.Fhir.ElementModel;
using Firely.Packaging.Binary;
using System.Diagnostics;
using System;

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

            var sw = new Stopwatch();

            var te = ElementNode.FromElement(sd.ToTypedElement());

            sw.Start();
            ExpandoObject expando = null;
            for (int i=0; i < 1000; i++)
                expando = te.ToExpando();
            sw.Stop();
            Console.WriteLine($"ToExpando() took {sw.ElapsedMilliseconds / 1000.0} ms");

            ReadOnlyMemory<byte> memory = null;
            sw.Restart();
            for(int i=0; i < 1000; i++)
                memory = PrimitiveObjectFormatterCore.SerializeToMemory(expando);
            sw.Stop();
            Console.WriteLine($"Serializing an expando took {sw.ElapsedMilliseconds / 1000.0} ms");

            object result = null;
            sw.Restart();
            for (int i = 0; i < 1000; i++)
                result = PrimitiveObjectFormatterCore.Deserialize(memory);
            sw.Stop();
            Console.WriteLine($"Deserializing to an expando took {sw.ElapsedMilliseconds / 1000.0} ms");

            dynamic dyn = result;
            Assert.AreEqual("normative", dyn.extension[1].value.value);
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
            dynamic parsed = PrimitiveObjectFormatterCore.Deserialize(memory);

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
