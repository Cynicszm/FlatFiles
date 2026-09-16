using System;
using System.Reflection;

namespace FlatFiles.TypeMapping
{
    internal sealed class FieldAccessor(FieldInfo fieldInfo, IMemberAccessor? parent) : IMemberAccessor
    {
        public MemberInfo MemberInfo => fieldInfo;

        public IMemberAccessor? ParentAccessor { get; } = parent;

        public string Name => ParentAccessor is null ? fieldInfo.Name : $"{ParentAccessor.Name}.{fieldInfo.Name}";

        public Type Type => fieldInfo.FieldType;

        public object? GetValue(object instance)
        {
            return fieldInfo.GetValue(instance);
        }

        public void SetValue(object instance, object? value)
        {
            fieldInfo.SetValue(instance, value);
        }
    }
}
