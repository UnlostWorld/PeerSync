// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.SyncProviders.CustomizePlus;

using System;

/// <summary>
/// Error codes returned by some API methods.
/// </summary>
public enum ErrorCodes
{
	Success = 0,

	/// <summary>
	/// Returned when invalid character address was provided.
	/// </summary>
	InvalidCharacter = 1,

	/// <summary>
	/// Returned if IPCCharacterProfile could not be deserialized or deserialized into an empty object.
	/// </summary>
	CorruptedProfile = 2,

	/// <summary>
	/// Provided character does not have active profiles, provided profile id is invalid or provided profile id is not valid for use in current function.
	/// </summary>
	ProfileNotFound = 3,

	/// <summary>
	/// General error telling that one of the provided arguments were invalid.
	/// </summary>
	InvalidArgument = 4,

	UnknownError = 255,
}

public static class ErrorCodeExtensions
{
	public static void ThrowOnFailure(this ErrorCodes self)
	{
		if (self == ErrorCodes.Success)
			return;

		throw new Exception($"Customize+ error: {self}");
	}
}