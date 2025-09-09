// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

using System.Collections.Generic;

namespace PeerSync.SyncProviders.Penumbra;

public class PenumbraData
	{
		public Dictionary<string, string> Files { get; set; } = new();
		public Dictionary<string, long> FileSizes { get; set; } = new();
		public Dictionary<string, string> Redirects { get; set; } = new();
		public string? MetaManipulations { get; set; }

		public bool IsSame(PenumbraData other)
		{
			if (this.Files.Count != other.Files.Count)
				return false;

			if (this.FileSizes.Count != other.FileSizes.Count)
				return false;

			if (this.Redirects.Count != other.Redirects.Count)
				return false;


			if (this.MetaManipulations != other.MetaManipulations)
				return false;

			foreach ((string key, string value) in this.Files)
			{
				if (!other.Files.TryGetValue(key, out string? otherValue)
				|| otherValue != value)
				{
					return false;
				}
			}

			foreach ((string key, long value) in this.FileSizes)
			{
				if (!other.FileSizes.TryGetValue(key, out long otherValue)
				|| otherValue != value)
				{
					return false;
				}
			}

			foreach ((string key, string value) in this.Redirects)
			{
				if (!other.Redirects.TryGetValue(key, out string? otherValue)
				|| otherValue != value)
				{
					return false;
				}
			}

			return true;
		}
	}