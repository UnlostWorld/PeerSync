// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.SyncProviders.CustomizePlus;

using System;

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