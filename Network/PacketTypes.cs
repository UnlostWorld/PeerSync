// This software is licensed under the GNU AFFERO GENERAL PUBLIC LICENSE v3

namespace PeerSync;

public enum PacketTypes : byte
{
	IAm = 1,
	CharacterData = 2,
	FileRequest = 3,
	FileData = 4,
}