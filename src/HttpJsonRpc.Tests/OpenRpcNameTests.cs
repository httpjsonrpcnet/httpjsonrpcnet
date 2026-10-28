using System;
using System.Threading.Tasks;
using HttpJsonRpc.Tests.Fixtures.OpenRpcNames;
using NUnit.Framework;

namespace HttpJsonRpc.Tests
{
    public class OpenRpcNameTests
    {
        [Test]
        public void ExplicitNameControlsComponentsRecursiveReferencesAndTaskResults()
        {
            var generator = new OpenRpcSchemaGenerator(new JsonRpcOptions());
            Assert.That(generator.GetSchema(typeof(PublicContact)).Ref, Is.EqualTo("#/components/schemas/Contact"));
            Assert.That(generator.GetSchema(typeof(Task<PublicContact>)).Ref, Is.EqualTo("#/components/schemas/Contact"));
            Assert.That(generator.Schemas.Count, Is.EqualTo(1));
            Assert.That(generator.Schemas["Contact"].Description, Is.EqualTo("A public contact."));
            Assert.That(generator.Schemas["Contact"].Properties["parent"].Ref, Is.EqualTo("#/components/schemas/Contact"));
        }

        [Test]
        public void MissingNamePreservesExistingIdentity()
        {
            var generator = new OpenRpcSchemaGenerator(new JsonRpcOptions());
            Assert.That(generator.GetName(typeof(UnnamedContact)), Is.EqualTo(typeof(UnnamedContact).FullName));
        }

        [Test]
        public void ConflictingNamesFailInsteadOfReusingAnotherType()
        {
            var generator = new OpenRpcSchemaGenerator(new JsonRpcOptions());
            generator.GetSchema(typeof(PublicContact));
            Assert.Throws<InvalidOperationException>(() => generator.GetSchema(typeof(ConflictingContact)));
        }

        [Test]
        public void InvalidComponentNameFailsBeforeCreatingReferences()
        {
            var generator = new OpenRpcSchemaGenerator(new JsonRpcOptions());
            Assert.Throws<InvalidOperationException>(() => generator.GetSchema(typeof(InvalidName)));
            Assert.That(generator.Schemas, Is.Empty);
        }
    }
}
