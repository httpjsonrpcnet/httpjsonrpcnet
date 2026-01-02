using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;

namespace HttpJsonRpc
{
    internal static class PropertyInfoExtensions
    {
        public static bool IsRequired(this PropertyInfo property)
        {
            if (property.IsDefined(typeof(RequiredAttribute))) return true;
            if (property.IsDefined(typeof(JsonRequiredAttribute))) return true;
            if (property.CustomAttributes.Any(a => a.AttributeType.FullName == "System.Runtime.CompilerServices.RequiredMemberAttribute")) return true;
            return false;
        }
    }
}
