// This file provides polyfill attributes that enable modern C# language features
// when targeting older .NET versions, like .NET Framework 4.7.2.
//
// These attributes are recognized by the C# compiler but don't exist in older framework versions.
// The compiler uses them to enable language features and perform compile-time analysis.
// They have NO runtime behavior, they're purely for compiler support.

#pragma warning disable IDE0130 // The namespace does not match the folder structure - this is intentional for polyfill attributes

namespace System.Diagnostics.CodeAnalysis
{
    /// <summary>
    /// C# 8.0 - Nullable Reference Types
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    internal sealed class NotNullWhenAttribute(bool returnValue) : Attribute
    {
        public bool ReturnValue { get; } = returnValue;
    }

    /// <summary>
    /// C# 8.0 - Nullable Reference Types
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    internal sealed class MaybeNullWhenAttribute(bool returnValue) : Attribute
    {
        public bool ReturnValue { get; } = returnValue;
    }

    /// <summary>
    /// C# 8.0 - Nullable Reference Types
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
    internal sealed class MemberNotNullAttribute(params string[] members) : Attribute
    {
        public string[] Members { get; } = members;
    }

    /// <summary>
    /// C# 8.0 - Nullable Reference Types
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
    internal sealed class MemberNotNullWhenAttribute(bool returnValue, params string[] members) : Attribute
    {
        public bool ReturnValue { get; } = returnValue;

        public string[] Members { get; } = members;
    }

    /// <summary>
    /// C# 8.0 - Nullable Reference Types
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.ReturnValue, AllowMultiple = true, Inherited = false)]
    internal sealed class NotNullAttribute : Attribute;

    /// <summary>
    /// C# 8.0 - Nullable Reference Types
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.ReturnValue, Inherited = false)]
    internal sealed class MaybeNullAttribute : Attribute;

    /// <summary>
    /// C# 8.0 - Nullable Reference Types
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    internal sealed class DoesNotReturnAttribute : Attribute;

    /// <summary>
    /// C# 8.0 - Nullable Reference Types
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    internal sealed class DoesNotReturnIfAttribute(bool parameterValue) : Attribute
    {
        public bool ParameterValue { get; } = parameterValue;
    }

    /// <summary>
    /// C# 11.0 - Required Members
    /// Requires: CompilerFeatureRequiredAttribute + RequiredMemberAttribute
    /// </summary>
    [AttributeUsage(AttributeTargets.Constructor, Inherited = false)]
    internal sealed class SetsRequiredMembersAttribute : Attribute;
}

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// C# 9.0 - Init-only Properties
    /// </summary>
    internal static class IsExternalInit;

    /// <summary>
    /// C# 11.0 - Required Members
    /// Requires: RequiredMemberAttribute + SetsRequiredMembersAttribute (for constructors)
    /// </summary>
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
    internal sealed class CompilerFeatureRequiredAttribute(string featureName) : Attribute
    {
        public string FeatureName { get; } = featureName;

        public bool IsOptional { get; set; }
    }

    /// <summary>
    /// C# 11.0 - Required Members
    /// Requires: CompilerFeatureRequiredAttribute + SetsRequiredMembersAttribute (for constructors)
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property | AttributeTargets.Field, Inherited = false)]
    internal sealed class RequiredMemberAttribute : Attribute;

    /// <summary>
    /// C# 10.0 - CallerArgumentExpression
    /// Captures the expression passed to a parameter as a string.
    /// Required: This attribute only
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    internal sealed class CallerArgumentExpressionAttribute(string parameterName) : Attribute
    {
        public string ParameterName { get; } = parameterName;
    }

    /// <summary>
    /// C# 10.0 - Interpolated String Handlers
    /// Marks a type as an interpolated string handler.
    /// Required: This attribute + InterpolatedStringHandlerArgumentAttribute (for parameters)
    /// </summary>
    /// <remarks>
    /// Used for custom string interpolation performance optimizations.
    /// Most applications don't need to create custom handlers.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
    internal sealed class InterpolatedStringHandlerAttribute : Attribute;

    /// <summary>
    /// C# 10.0 - Interpolated String Handlers
    /// Indicates which arguments from the method should be passed to the interpolated string handler.
    /// Required: InterpolatedStringHandlerAttribute on the handler type
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    internal sealed class InterpolatedStringHandlerArgumentAttribute : Attribute
    {
        public InterpolatedStringHandlerArgumentAttribute(string argument)
        {
            Arguments = [argument];
        }

        public InterpolatedStringHandlerArgumentAttribute(params string[] arguments)
        {
            Arguments = arguments;
        }

        public string[] Arguments { get; }
    }
}