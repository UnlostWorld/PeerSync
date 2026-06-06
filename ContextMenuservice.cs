// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.Characters;

using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Text;
using PeerSync.Connections;

public class ContextMenuService : IDisposable
{
	public ContextMenuService()
	{
		Plugin.XivContextMenu.OnMenuOpened += this.OnContextMenuOpened;
	}

	public void Dispose()
	{
		Plugin.XivContextMenu.OnMenuOpened -= this.OnContextMenuOpened;
	}

	private void OnContextMenuOpened(IMenuOpenedArgs args)
	{
		if (args.Target is not MenuTargetDefault target)
			return;

		if (target.TargetObject is IPlayerCharacter character)
		{
			this.Show(character, ref args);
		}
	}

	private void Show(IPlayerCharacter character, ref IMenuOpenedArgs args)
	{
		List<MenuItem> menus = new();

		string characterName = character.Name.ToString();
		string characterWorld = character.HomeWorld.Value.Name.ToString();
		Configuration.Peer? peer = Configuration.Current.GetFriend(characterName, characterWorld);

		if (peer == null)
		{
			MenuItem addFriendMenu = new();
			addFriendMenu.Name = SeStringUtils.ToSeString("Add Friend");
			addFriendMenu.OnClicked = (a) => Plugin.Ui.AddPeerWindow.Show(characterName, characterWorld);
			menus.Add(addFriendMenu);
		}

		CharacterConnection connection = Plugin.Connections.GetOrCreate(character);
		if (connection.IsConnected)
		{
			MenuItem inspectMenu = new();
			inspectMenu.Name = SeStringUtils.ToSeString("Inspect");
			inspectMenu.OnClicked = (a) => Plugin.Ui.InspectWindow.Show(connection);
			menus.Add(inspectMenu);

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
		item.Prefix = connection.IsConnected ? SeIconChar.ExperienceFilled : SeIconChar.Experience;
		item.OnClicked = (e) => e.OpenSubmenu(menus);
		args.AddMenuItem(item);
	}
}