using System.Collections.Generic;

namespace Sekai
{
	public class ChainDialogPlayer
	{
		private readonly Queue<DialogBase> _dialogs = new Queue<DialogBase>();

		public void Enqueue(DialogBase dialog)
		{
			if (dialog != null)
			{
				_dialogs.Enqueue(dialog);
			}
		}

		public void Play()
		{
			if (_dialogs.Count > 0)
			{
				_dialogs.Dequeue().Open();
			}
		}
	}
}
