// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using Dalamud.Game.Text.SeStringHandling;

namespace PeerSync;

public static class SeStringUtils
{
	public static SeString ToSeString(string str)
	{
		return new SeStringBuilder().AddText(str).Build();
	}
}