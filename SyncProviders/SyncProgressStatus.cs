// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.SyncProviders;

using Dalamud.Interface;

public enum SyncProgressStatus
{
	None,
	Syncing,
	Applied,
	Empty,
	NotApplied,
	Error,
}

public static class SyncProgressStatusExtensions
{
	public static FontAwesomeIcon GetIcon(this SyncProgressStatus status)
	{
		switch (status)
		{
			case SyncProgressStatus.None: return FontAwesomeIcon.None;
			case SyncProgressStatus.Syncing: return FontAwesomeIcon.Sync;
			case SyncProgressStatus.Applied: return FontAwesomeIcon.Check;
			case SyncProgressStatus.Empty: return FontAwesomeIcon.None;
			case SyncProgressStatus.NotApplied: return FontAwesomeIcon.Times;
			case SyncProgressStatus.Error: return FontAwesomeIcon.ExclamationTriangle;
		}

		return FontAwesomeIcon.None;
	}
}
