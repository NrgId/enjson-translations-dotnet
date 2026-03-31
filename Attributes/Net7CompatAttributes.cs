// Source - https://stackoverflow.com/a/75995697
// Posted by m1o2
// Retrieved 2026-03-26, License - CC BY-SA 4.0
//
// These are to allow support of `required` and `init` in classes/records without targeting .NET 7

// ReSharper disable once CheckNamespace - NET 7 compat requries this namespace
namespace System.Runtime.CompilerServices
{
	/// <summary>
	/// Indicates that compiler support for a particular feature is required for the location where this attribute is applied.
	/// </summary>
	[AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
	public sealed class CompilerFeatureRequiredAttribute(string featureName) : Attribute
	{
		/// <summary>
		/// The name of the compiler feature.
		/// </summary>
		public string FeatureName { get; } = featureName;

		/// <summary>
		/// If true, the compiler can choose to allow access to the location where this attribute is applied if it does not understand <see cref="FeatureName"/>.
		/// </summary>
		public bool IsOptional { get; init; }

		/// <summary>
		/// The <see cref="FeatureName"/> used for the ref structs C# feature.
		/// </summary>
		public const string RefStructs = nameof(RefStructs);

		/// <summary>
		/// The <see cref="FeatureName"/> used for the required members C# feature.
		/// </summary>
		public const string RequiredMembers = nameof(RequiredMembers);
	}

	/// <summary>Specifies that a type has required members or that a member is required.</summary>
	[AttributeUsage(
		AttributeTargets.Class
			| AttributeTargets.Struct
			| AttributeTargets.Field
			| AttributeTargets.Property,
		AllowMultiple = false,
		Inherited = false
	)]
#if SYSTEM_PRIVATE_CORELIB
	public
#else
	internal
#endif
	sealed class RequiredMemberAttribute : Attribute { }

#if SYSTEM_PRIVATE_CORELIB
	public
#else
	internal
#endif
	static class IsExternalInit { }
}

namespace System.Diagnostics.CodeAnalysis
{
	/// <summary>
	/// Specifies that this constructor sets all required members for the current type, and callers
	/// do not need to set any required members themselves.
	/// </summary>
	[AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
#if SYSTEM_PRIVATE_CORELIB
	public
#else
	internal
#endif
	sealed class SetsRequiredMembersAttribute : Attribute { }
}
