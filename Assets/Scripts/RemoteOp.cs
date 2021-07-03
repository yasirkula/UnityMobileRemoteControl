public enum RemoteOpType
{
	CheckVolume = 0,
	ChangeVolume = 1,
	TriggerKey = 2
}

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