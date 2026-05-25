// .______ _____ ___________   _______   ___   _ _____
//  | ___ \  ___|  ___| ___ \ /  ___\ \ / / \ | /  __ \
//  | |_/ / |__ | |__ | |_/ / \ `--. \ V /|  \| | /  \/
//  |  __/|  __||  __||    /   `--. \ \ / | . ` | |
//  | |   | |___| |___| |\ \  /\__/ / | | | |\  | \__/
//  \_|   \____/\____/\_| \_| \____/  \_/ \_| \_/\____/
//  This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync.SyncProviders;

using System;
using System.Collections.Generic;
using PeerSync.Connections;
using PeerSync.SyncProviders.CustomizePlus;
using PeerSync.SyncProviders.Glamourer;
using PeerSync.SyncProviders.Honorific;
using PeerSync.SyncProviders.Moodles;
using PeerSync.SyncProviders.Penumbra;
using PeerSync.SyncProviders.PetNames;
using PeerSync.SyncProviders.SimpleHeels;

public class SyncService : IDisposable
{
	public readonly List<SyncProviderBase> Providers = new();
	private readonly Dictionary<string, SyncProviderBase> providerLookup = new();

	public SyncService()
	{
		lock (this.Providers)
		{
			this.Providers.Clear();
			this.Providers.Add(new CustomizePlusSync());
			this.Providers.Add(new MoodlesSync());
			this.Providers.Add(new HonorificSync());
			this.Providers.Add(new GlamourerSync());
			this.Providers.Add(new PenumbraSync());
			this.Providers.Add(new PetNamesSync());
			this.Providers.Add(new SimpleHeelsSync());
		}

		foreach (SyncProviderBase provider in this.Providers)
		{
			this.providerLookup.Add(provider.Key, provider);
		}
	}

	public SyncProviderBase? GetProvider(string key)
	{
		if (this.providerLookup.TryGetValue(key, out var provider))
			return provider;

		return null;
	}

	public List<SyncProgressBase> GetProgress(CharacterConnection character)
	{
		List<SyncProgressBase> progresses = new();

		lock (this.Providers)
		{
			foreach (SyncProviderBase sync in this.Providers)
			{
				SyncProgressBase? progress = sync.GetProgress(character);
				if (progress != null)
				{
					progresses.Add(progress);
				}
			}
		}

		return progresses;
	}

	public void Dispose()
	{
		lock (this.Providers)
		{
			foreach (SyncProviderBase sync in this.Providers)
			{
				sync.Dispose();
			}

			this.Providers.Clear();
		}

		this.providerLookup.Clear();
	}
}