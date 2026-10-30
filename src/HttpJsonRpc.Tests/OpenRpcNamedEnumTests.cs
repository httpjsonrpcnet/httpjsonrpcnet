using System;
using System.Linq;
using System.Threading.Tasks;
using HttpJsonRpc.Tests.Fixtures.OpenRpcNames;
using NUnit.Framework;

namespace HttpJsonRpc.Tests
{
    public class OpenRpcNamedEnumTests
    {
        [Test]
        public void NamedEnumSharesComponentAcrossNullableAndTaskUsages()
        {
            var options = new JsonRpcOptions();
            var count = 0;
            options.OpenRpc.SchemaTransformers.Add(c => { if (c.ConvertedType == typeof(Status)) count++; });
            var generator = new OpenRpcSchemaGenerator(options);
            Assert.That(generator.GetSchema(typeof(Status)).Ref, Is.EqualTo("#/components/schemas/Status"));
            Assert.That(generator.GetSchema(typeof(Task<Status>)).Ref, Is.EqualTo("#/components/schemas/Status"));
            var nullable = generator.GetSchema(typeof(Status?));
            Assert.That(nullable.AnyOf[0].Ref, Is.EqualTo("#/components/schemas/Status"));
            Assert.That(nullable.AnyOf[1].Type, Is.EqualTo("null"));
            Assert.That(generator.Schemas.Count, Is.EqualTo(1));
            Assert.That(count, Is.EqualTo(1));
            Assert.That(generator.Schemas["Status"].Description, Is.EqualTo("Status"));
            Assert.That(generator.Schemas["Status"].Type, Is.EqualTo("string"));
            Assert.That(generator.Schemas["Status"].Enum.Select(v => v.ToString()), Is.EqualTo(new[] { "Pending", "Complete" }));
        }

        [Test]
        public void UnnamedEnumStaysInline()
        {
            var generator = new OpenRpcSchemaGenerator(new JsonRpcOptions());
            Assert.That(generator.GetSchema(typeof(DayOfWeek)).Enum, Has.Length.EqualTo(7));
            Assert.That(generator.Schemas, Is.Empty);
        }
    }
}
