// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync;

using System.Collections.Generic;
using PeerSync.Network;
using PeerSync.Online;

public class GroupSync
{
	public readonly Configuration.Group Group;

	public readonly Dictionary<string, ServerStatus?> ServerStatus = new();
	public readonly Dictionary<string, CharacterSync> CharacterSyncs = new();
	public readonly HashSet<string> MemberFingerprints = new();
	private readonly Dictionary<string, string> testFingerprints = new();

	public GroupSync(Configuration.Group group)
	{
		this.Group = group;
	}

	public CharacterSync? TrySync(ConnectionManager network, string characterName, string world, ushort objectIndex)
	{
		string compoundName = $"{characterName}@{world}";

		if (!this.testFingerprints.ContainsKey(compoundName))
			this.testFingerprints[compoundName] = this.Group.GetMemberFingerprint(characterName, world);

		string memberFingerprint = this.testFingerprints[compoundName];

		if (!this.MemberFingerprints.Contains(memberFingerprint))
			return null;

		return new CharacterSync(network, this.Group, memberFingerprint, characterName, world, objectIndex);
	}
}