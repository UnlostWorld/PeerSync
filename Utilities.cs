// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync;

using Dalamud.Game.Text.SeStringHandling;

public static class SeStringUtils
{
	public static SeString ToSeString(string str)
	{
		return new SeStringBuilder().AddText(str).Build();
	}
}