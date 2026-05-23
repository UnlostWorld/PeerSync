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
	public readonly HashSet<string> MemberFingerprints = new();

	public GroupSync(Configuration.Group group)
	{
		this.Group = group;
	}
}