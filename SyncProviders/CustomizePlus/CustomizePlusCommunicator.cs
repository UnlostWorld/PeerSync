// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using System;

namespace PeerSync.SyncProviders.CustomizePlus;

public class CustomizePlusCommunicator : PluginCommunicatorBase
{
	protected override string InternalName => "CustomizePlus";
	protected override Version Version => new Version(2, 0, 0, 0);

	public Guid? GetActiveProfileIdOnCharacter(ushort objectIndex)
	{
		(int ec, Guid? guid) = this.InvokeFunc<(int, Guid?), ushort>(
			"CustomizePlus.Profile.GetActiveProfileIdOnCharacter",
			objectIndex);

		ErrorCodes errorCode = (ErrorCodes)ec;
		if (errorCode == ErrorCodes.ProfileNotFound)
			return null;

		errorCode.ThrowOnFailure();

		return guid;
	}

	public string? GetProfileByUniqueId(Guid guid)
	{
		(int ec, string? profile) = this.InvokeFunc<(int, string?), Guid>(
			"CustomizePlus.Profile.GetByUniqueId",
			guid);

		ErrorCodes errorCode = (ErrorCodes)ec;
		if (errorCode == ErrorCodes.ProfileNotFound)
			return null;

		errorCode.ThrowOnFailure();

		return profile;
	}

	public Guid? SetTemporaryProfileOnCharacter(ushort objectIndex, string content)
	{
		(int ec, Guid? guid) = this.InvokeFunc<(int, Guid?), ushort, string>(
			"CustomizePlus.Profile.SetTemporaryProfileOnCharacter",
			objectIndex,
			content);

		ErrorCodes errorCode = (ErrorCodes)ec;
		errorCode.ThrowOnFailure();

		return guid;
	}

	public void DeleteTemporaryProfileByUniqueId(Guid guid)
	{
		int ec = this.InvokeFunc<int, Guid>(
			"CustomizePlus.Profile.DeleteTemporaryProfileByUniqueId",
			guid);

		ErrorCodes errorCode = (ErrorCodes)ec;
		errorCode.ThrowOnFailure();
	}
}

/// <summary>
/// Error codes returned by some API methods
/// </summary>
public enum ErrorCodes
{
	Success = 0,

	/// <summary>
	/// Returned when invalid character address was provided
	/// </summary>
	InvalidCharacter = 1,

	/// <summary>
	/// Returned if IPCCharacterProfile could not be deserialized or deserialized into an empty object
	/// </summary>
	CorruptedProfile = 2,

	/// <summary>
	/// Provided character does not have active profiles, provided profile id is invalid or provided profile id is not valid for use in current function
	/// </summary>
	ProfileNotFound = 3,

	/// <summary>
	/// General error telling that one of the provided arguments were invalid.
	/// </summary>
	InvalidArgument = 4,

	UnknownError = 255
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