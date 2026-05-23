// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using Dalamud.Game.ClientState.Objects.SubKinds;

public static class IPlayerCharacterExtensions
{
	public static string GetId(this IPlayerCharacter self)
	{
		return $"{self.Name}@{self.HomeWorld.Value.Name.ToString()}";
	}

	public static string GetName(this IPlayerCharacter self)
	{
		return self.Name.TextValue;
	}

	public static string GetHomeWorld(this IPlayerCharacter self)
	{
		return self.HomeWorld.Value.Name.ToString();
	}
}