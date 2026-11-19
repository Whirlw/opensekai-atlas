namespace Sekai.Live
{
	public enum NoteDirection
	{
		Default = 0,
		Left = 1,
		Right = 2,
		Down = 3,
		DownLeft = 4,
		DownRight = 5
	}

	public static class NoteDirectionExtensions
	{
		public static NoteDirection Mirrored(this NoteDirection direction) => direction switch
		{
			NoteDirection.Left => NoteDirection.Right,
			NoteDirection.Right => NoteDirection.Left,
			NoteDirection.DownLeft => NoteDirection.DownRight,
			NoteDirection.DownRight => NoteDirection.DownLeft,
			_ => direction
		};
	}
}
