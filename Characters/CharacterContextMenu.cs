// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.Characters;

using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Gui.ContextMenu;
using PeerSync.Connections;

public static class CharacterContextMenu
{
	public static void Show(IPlayerCharacter character, ref IMenuOpenedArgs args)
	{
		string characterName = character.Name.ToString();
		string characterWorld = character.HomeWorld.Value.Name.ToString();
		Configuration.Peer? peer = Configuration.Current.GetFriend(characterName, characterWorld);

		if (peer == null)
		{
			args.AddMenuItem(new MenuItem()
			{
				Name = SeStringUtils.ToSeString("Add peer"),
				OnClicked = (a) =>
				{
					Plugin.Instance?.AddPeerWindow.Show(characterName, characterWorld);
				},
				UseDefaultPrefix = false,
				PrefixChar = 'S',
				PrefixColor = 526,
			});
		}

		CharacterConnection connection = Plugin.Connections.GetOrCreate(character);
		if (connection.IsConnected)
		{
			/*MenuItem item = new MenuItem();
			item.Name = SeStringUtils.ToSeString("Peer Sync");
			item.IsSubmenu = true;
			item.PrefixChar = 'S';
			item.PrefixColor = 526;
			args.AddMenuItem(item);*/

			args.AddMenuItem(new MenuItem()
			{
				Name = SeStringUtils.ToSeString("Reset"),
				OnClicked = (a) =>
				{
					connection.Reset();
				},
				UseDefaultPrefix = false,
				PrefixChar = 'S',
				PrefixColor = 526,
			});
		}
	}
}