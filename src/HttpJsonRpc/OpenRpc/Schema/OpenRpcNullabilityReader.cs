using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HttpJsonRpc
{
    // Reads the compiler's nullable-metadata encoding without requiring its attribute types
    // or NullabilityInfoContext on the target framework. Unknown is deliberately preserved.
    internal static class OpenRpcNullabilityReader
    {
        private const string Prefix = "System.Runtime.CompilerServices.";

        internal static OpenRpcNullabilityInfo Unknown(Type type) => Read(type, Array.Empty<CustomAttributeData>(), 0, null);

        internal static OpenRpcNullabilityInfo Read(Type type, MemberInfo member, ParameterInfo parameter,
            OpenRpcNullabilityInfo containing = null)
        {
            var owner = member ?? parameter?.Member;
            var declaring = owner?.DeclaringType;
            var map = GetMap(FindDeclaring(containing, declaring));
            // Reflection on a constructed generic type substitutes T before we can read its flags.
            // Decode the definition instead, then substitute the contextual generic arguments.
            if (declaring?.IsGenericType == true)
            {
                var definition = declaring.GetGenericTypeDefinition();
                var original = definition.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                    .FirstOrDefault(m => m.MetadataToken == owner.MetadataToken);
                if (original != null)
                {
                    owner = original;
                    if (parameter != null && original is MethodInfo method)
                        parameter = parameter.Position < 0 ? method.ReturnParameter : method.GetParameters()[parameter.Position];
                    else member = original;
                }
            }
            var declared = parameter?.ParameterType ?? (member as PropertyInfo)?.PropertyType ?? (member as FieldInfo)?.FieldType ?? type;
            var attributes = parameter != null ? parameter.GetCustomAttributesData() : member?.GetCustomAttributesData() ?? Array.Empty<CustomAttributeData>();
            return Read(declared, attributes, Context(owner), map);
        }

        internal static OpenRpcNullabilityInfo FindDeclaring(OpenRpcNullabilityInfo current, Type target)
        {
            if (target == null) return current;
            while (current != null)
            {
                if (current.Type == target || (current.Type.IsGenericType && target.IsGenericType &&
                    current.Type.GetGenericTypeDefinition() == target.GetGenericTypeDefinition())) return current;
                var definition = current.Type.IsGenericType ? current.Type.GetGenericTypeDefinition() : current.Type;
                if (definition.BaseType == null) break;
                current = Read(definition.BaseType, definition.GetCustomAttributesData(), Context(definition), GetMap(current));
            }
            return Unknown(target);
        }

        internal static OpenRpcNullabilityInfo CollectionArgument(OpenRpcNullabilityInfo current, Type collection, int index)
        {
            if (current.Type.IsArray) return current.Arguments[0];
            if (current.Type == collection) return current.Arguments[index];
            // Read the annotated base before falling back to an implemented interface.
            var definition = current.Type.IsGenericType ? current.Type.GetGenericTypeDefinition() : current.Type;
            if (definition.BaseType != null && definition.BaseType != typeof(object) && collection.IsAssignableFrom(current.Type.BaseType))
            {
                var parent = Read(definition.BaseType, definition.GetCustomAttributesData(), Context(definition), GetMap(current));
                return CollectionArgument(parent, collection, index);
            }
            var contract = definition.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == collection.GetGenericTypeDefinition());
            return contract == null ? Unknown(collection.GetGenericArguments()[index]) :
                Read(contract.GetGenericArguments()[index], Array.Empty<CustomAttributeData>(), 0, GetMap(current));
        }

        private static Dictionary<Type, OpenRpcNullabilityInfo> GetMap(OpenRpcNullabilityInfo info)
        {
            var map = new Dictionary<Type, OpenRpcNullabilityInfo>();
            if (info?.Type.IsGenericType == true)
            {
                var parameters = info.Type.GetGenericTypeDefinition().GetGenericArguments();
                for (var i = 0; i < parameters.Length; i++) map[parameters[i]] = info.Arguments[i];
            }
            return map;
        }

        private static byte Context(MemberInfo owner)
        {
            for (var scope = owner; scope != null; scope = scope.DeclaringType)
            {
                var attribute = scope.GetCustomAttributesData().FirstOrDefault(a => a.AttributeType.FullName == Prefix + "NullableContextAttribute");
                if (attribute?.ConstructorArguments.Count == 1 && attribute.ConstructorArguments[0].Value is byte value) return value;
            }
            return 0;
        }

        private static OpenRpcNullabilityInfo Read(Type type, IEnumerable<CustomAttributeData> attributes, byte context,
            Dictionary<Type, OpenRpcNullabilityInfo> map)
        {
            var attribute = attributes.FirstOrDefault(a => a.AttributeType.FullName == Prefix + "NullableAttribute");
            byte[] flags = null;
            var repeated = true;
            if (attribute?.ConstructorArguments.Count == 1)
            {
                var value = attribute.ConstructorArguments[0].Value;
                if (value is byte single) flags = new[] { single };
                else if (value is IList<CustomAttributeTypedArgument> values)
                { flags = values.Select(v => v.Value is byte b ? b : (byte)0).ToArray(); repeated = false; }
            }
            var position = 0;
            return Decode(type, flags, repeated, context, ref position, map);
        }

        private static OpenRpcNullabilityInfo Decode(Type type, byte[] flags, bool repeated, byte context, ref int position,
            Dictionary<Type, OpenRpcNullabilityInfo> map)
        {
            if (type.IsByRef) return Decode(type.GetElementType(), flags, repeated, context, ref position, map);
            var underlying = System.Nullable.GetUnderlyingType(type);
            if (underlying != null)
            {
                var argument = Decode(underlying, flags, repeated, context, ref position, map);
                return new OpenRpcNullabilityInfo(typeof(Nullable<>).MakeGenericType(argument.Type), true, argument);
            }
            byte flag = 0;
            if (!type.IsValueType || type.IsGenericType)
            {
                flag = flags == null ? context : flags.Length == 0 ? (byte)0 : repeated ? flags[0] : position < flags.Length ? flags[position] : (byte)0;
                position++;
            }
            bool? nullable = type.IsValueType ? false : flag == 2 ? true : flag == 1 ? false : (bool?)null;
            if (type.IsGenericParameter && map != null && map.TryGetValue(type, out var substitution))
                return flag == 2 && !substitution.Type.IsValueType ? substitution.WithNullable(true) : substitution;
            var arguments = new List<OpenRpcNullabilityInfo>();
            if (type.IsArray) arguments.Add(Decode(type.GetElementType(), flags, repeated, context, ref position, map));
            else if (type.IsGenericType)
                foreach (var argument in type.GetGenericArguments()) arguments.Add(Decode(argument, flags, repeated, context, ref position, map));
            if (type.IsArray)
                type = type.GetArrayRank() == 1 ? arguments[0].Type.MakeArrayType() : arguments[0].Type.MakeArrayType(type.GetArrayRank());
            else if (type.IsGenericType) type = type.GetGenericTypeDefinition().MakeGenericType(arguments.Select(a => a.Type).ToArray());
            return new OpenRpcNullabilityInfo(type, nullable, arguments.ToArray());
        }
    }
}
