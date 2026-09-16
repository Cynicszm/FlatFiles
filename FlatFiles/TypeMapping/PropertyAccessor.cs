using System;
using System.Reflection;

namespace FlatFiles.TypeMapping
{
    internal sealed class PropertyAccessor(PropertyInfo propertyInfo, IMemberAccessor? parent) : IMemberAccessor
    {
        public MemberInfo MemberInfo => propertyInfo;

        public IMemberAccessor? ParentAccessor { get; } = parent;

        public string Name => ParentAccessor == null ? propertyInfo.Name : $"{ParentAccessor.Name}.{propertyInfo.Name}";

        public Type Type => propertyInfo.PropertyType;

        public object? GetValue(object instance)
        {
            return propertyInfo.GetValue(instance);
        }

        public void SetValue(object instance, object? value)
        {
            propertyInfo.SetValue(instance, value);
        }
    }
}
