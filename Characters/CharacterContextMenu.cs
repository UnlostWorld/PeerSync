// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.Characters;

using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Gui.ContextMenu;
using PeerSync.Connections;

public static class CharacterContextMenu
{
	public static void Show(IPlayerCharacter character, ref IMenuOpenedArgs args)
	{
		List<MenuItem> menus = new();

		string characterName = character.Name.ToString();
		string characterWorld = character.HomeWorld.Value.Name.ToString();
		Configuration.Peer? peer = Configuration.Current.GetFriend(characterName, characterWorld);

		if (peer == null)
		{
			MenuItem addFriendMenu = new();
			addFriendMenu.Name = SeStringUtils.ToSeString("Add Friend");
			addFriendMenu.OnClicked = (a) => Plugin.Instance?.AddPeerWindow.Show(characterName, characterWorld);
			menus.Add(addFriendMenu);
		}

		CharacterConnection connection = Plugin.Connections.GetOrCreate(character);
		if (connection.IsConnected)
		{
			MenuItem resetMenu = new();
			resetMenu.Name = SeStringUtils.ToSeString("Reset");
			resetMenu.OnClicked = (a) => connection.Reset();
			menus.Add(resetMenu);

			MenuItem blockMenu = new();
			if (Configuration.Current.GetIsBlocked(characterName, characterWorld))
			{
				blockMenu.Name = SeStringUtils.ToSeString("Unblock");
				blockMenu.OnClicked = (a) =>
				{
					Configuration.Current.SetIsBlocked(characterName, characterWorld, false);
					connection.Reset();
				};
			}
			else
			{
				blockMenu.Name = SeStringUtils.ToSeString("Block");
				blockMenu.OnClicked = (a) =>
				{
					Configuration.Current.SetIsBlocked(characterName, characterWorld, true);
					connection.Reset();
				};
			}

			menus.Add(blockMenu);
		}

		MenuItem item = new();
		item.Name = SeStringUtils.ToSeString("Peer Sync");
		item.IsSubmenu = true;
		item.PrefixChar = 'S';
		item.PrefixColor = 526;
		item.OnClicked = (e) => e.OpenSubmenu(menus);
		args.AddMenuItem(item);
	}
}