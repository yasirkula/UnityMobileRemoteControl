public enum RemoteOpType
{
	CheckVolume,
	SetVolume,
	IncrementVolume,
	TriggerKey,
	TriggerKeyboardInput,
	TriggerMouseMovement,
	TriggerMouseButtonDown,
	TriggerMouseButtonUp,
	TriggerMouseWheel,
	RequestMouseScreenshot
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

[System.Serializable]
public struct MouseDelta // Vector2Int doesn't seem to support JsonUtility so this class is a kind of replacement for it
{
	public int x, y;

	public MouseDelta( int x, int y )
	{
		this.x = x;
		this.y = y;
	}
}

[System.Serializable]
public struct KeyboardInput
{
	public int backspace;
	public string text;

	public KeyboardInput( int backspace, string text )
	{
		this.backspace = backspace;
		this.text = text;
	}
}