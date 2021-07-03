public enum RemoteOpType
{
	CheckVolume = 0,
	ChangeVolume = 1,
	TriggerKey = 2,
	TriggerMouseMovement = 3,
	TriggerMouseButtonDown = 4,
	TriggerMouseButtonUp = 5
}

[System.Serializable]
public struct RemoteOp
{
	public RemoteOpType Type;
	public string Data;

	public RemoteOp( RemoteOpType type, string data )
	{
		Type = type;
		Data = data;
	}
}