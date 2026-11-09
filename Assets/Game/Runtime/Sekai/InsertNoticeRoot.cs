using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Beebyte.Obfuscator;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.CompilerServices;
using UnityEngine;

namespace Sekai
{
	public class InsertNoticeRoot : MonoBehaviour
	{
		public enum AnimeState
		{
			None = 0,
			In = 1,
			Move = 2,
			Out = 3
		}

		[StructLayout((LayoutKind)3)]
		[CompilerGenerated]
		private struct _003CMoveAnime_003Ed__26 : IAsyncStateMachine
		{
			public int _003C_003E1__state;

			public AsyncUniTaskMethodBuilder _003C_003Et__builder;

			public InsertNoticeRoot _003C_003E4__this;

			public Vector3 from;

			public Vector3 to;

			private UniTask.Awaiter _003C_003Eu__1;

			private void MoveNext()
			{
				throw null;
			}

			void IAsyncStateMachine.MoveNext()
			{
				//ILSpy generated this explicit interface implementation from .override directive in MoveNext
				this.MoveNext();
			}

			[DebuggerHidden]
			private void SetStateMachine(IAsyncStateMachine stateMachine)
			{
				throw null;
			}

			void IAsyncStateMachine.SetStateMachine(IAsyncStateMachine stateMachine)
			{
				//ILSpy generated this explicit interface implementation from .override directive in SetStateMachine
				this.SetStateMachine(stateMachine);
			}
		}

		[StructLayout((LayoutKind)3)]
		[CompilerGenerated]
		private struct _003CPlayInAnime_003Ed__27 : IAsyncStateMachine
		{
			public int _003C_003E1__state;

			public AsyncUniTaskMethodBuilder _003C_003Et__builder;

			public InsertNoticeRoot _003C_003E4__this;

			private UniTask.Awaiter _003C_003Eu__1;

			private void MoveNext()
			{
				throw null;
			}

			void IAsyncStateMachine.MoveNext()
			{
				//ILSpy generated this explicit interface implementation from .override directive in MoveNext
				this.MoveNext();
			}

			[DebuggerHidden]
			private void SetStateMachine(IAsyncStateMachine stateMachine)
			{
				throw null;
			}

			void IAsyncStateMachine.SetStateMachine(IAsyncStateMachine stateMachine)
			{
				//ILSpy generated this explicit interface implementation from .override directive in SetStateMachine
				this.SetStateMachine(stateMachine);
			}
		}

		[StructLayout((LayoutKind)3)]
		[CompilerGenerated]
		private struct _003CPlayOutAnime_003Ed__28 : IAsyncStateMachine
		{
			public int _003C_003E1__state;

			public AsyncUniTaskMethodBuilder _003C_003Et__builder;

			public InsertNoticeRoot _003C_003E4__this;

			private UniTask.Awaiter _003C_003Eu__1;

			private void MoveNext()
			{
				throw null;
			}

			void IAsyncStateMachine.MoveNext()
			{
				//ILSpy generated this explicit interface implementation from .override directive in MoveNext
				this.MoveNext();
			}

			[DebuggerHidden]
			private void SetStateMachine(IAsyncStateMachine stateMachine)
			{
				throw null;
			}

			void IAsyncStateMachine.SetStateMachine(IAsyncStateMachine stateMachine)
			{
				//ILSpy generated this explicit interface implementation from .override directive in SetStateMachine
				this.SetStateMachine(stateMachine);
			}
		}

		[SerializeField]
		private TweenPosition _inTweenPos;

		[SerializeField]
		private TweenPosition _outTweenPos;

		[SerializeField]
		private TweenAlpha _alphaTweener;

		[SerializeField]
		private float _moveAnimationSec;

		[SerializeField]
		private AchivedMissionNoticeCell _achivedMissionNotice;

		[SerializeField]
		private FriendRequestNoticeCell _friendRequestNotice;

		[SerializeField]
		private FriendInviteNoticeCell _friendInviteNotice;

		[SerializeField]
		private LiveBonusNoticeCell _liveBonusNotice;

		[SerializeField]
		private OtherNoticeCountCell _otherNoticeCountCell;

		private AnimeState _latestAnimeState;

		public AchivedMissionNoticeCell AchivedMissionNotice
		{
			get
			{
				return _achivedMissionNotice;
			}
		}

		public FriendRequestNoticeCell FriendRequestNotice
		{
			get
			{
				return _friendRequestNotice;
			}
		}

		public FriendInviteNoticeCell FriendInviteNotice
		{
			get
			{
				return _friendInviteNotice;
			}
		}

		public OtherNoticeCountCell OtherNoticeCountCell
		{
			get
			{
				return _otherNoticeCountCell;
			}
		}

		public LiveBonusNoticeCell LiveBonusNoticeCell
		{
			get
			{
				return _liveBonusNotice;
			}
		}

		public AnimeState LatestAnimeState
		{
			[CompilerGenerated]
			get
			{
				return _latestAnimeState;
			}
			[CompilerGenerated]
			private set
			{
				_latestAnimeState = value;
			}
		}

		[Skip]
		public void Refresh()
		{
			SetNoticeActive(_achivedMissionNotice, false);
			SetNoticeActive(_friendRequestNotice, false);
			SetNoticeActive(_otherNoticeCountCell, false);
			SetNoticeActive(_liveBonusNotice, false);
			SetNoticeActive(_friendInviteNotice, false);
			ResetTween();
		}

		public void ResetTween()
		{
			if (_inTweenPos != null && transform is RectTransform rectTransform)
			{
				rectTransform.anchoredPosition = new Vector2(_inTweenPos.From.x, 0f);
			}

			if (_alphaTweener != null)
			{
				_alphaTweener.Stop();
				if (_alphaTweener.TweenTarget == null)
				{
					_alphaTweener.SetupTarget();
				}

				if (_alphaTweener.TweenTarget != null && _alphaTweener.TweenTarget.IsExist)
				{
					_alphaTweener.TweenTarget.Alpha = _alphaTweener.From;
				}
			}

			LatestAnimeState = AnimeState.None;
		}

		[AsyncStateMachine(typeof(_003CMoveAnime_003Ed__26))]
		public UniTask MoveAnime(Vector3 from, Vector3 to)
		{
			if (transform is RectTransform rectTransform)
			{
				rectTransform.anchoredPosition = to;
			}
			else
			{
				transform.localPosition = to;
			}

			LatestAnimeState = AnimeState.Move;
			return UniTask.CompletedTask;
		}

		[AsyncStateMachine(typeof(_003CPlayInAnime_003Ed__27))]
		public UniTask PlayInAnime()
		{
			LatestAnimeState = AnimeState.In;
			_inTweenPos?.Play(TweenBase.PlayDirection.Forward);
			_alphaTweener?.Play(TweenBase.PlayDirection.Forward);
			return UniTask.CompletedTask;
		}

		[AsyncStateMachine(typeof(_003CPlayOutAnime_003Ed__28))]
		public UniTask PlayOutAnime()
		{
			LatestAnimeState = AnimeState.Out;
			_outTweenPos?.Play(TweenBase.PlayDirection.Forward);
			_alphaTweener?.Play(TweenBase.PlayDirection.Back);
			return UniTask.CompletedTask;
		}

		private void RefreshHorizontalTweener(ref TweenPosition tweener)
		{
			if (tweener == null)
			{
				return;
			}

			var from = tweener.From;
			var to = tweener.To;
			from.y = 0f;
			to.y = 0f;
			tweener.From = from;
			tweener.To = to;
		}

		private static void SetNoticeActive(Component component, bool active)
		{
			if (component != null)
			{
				component.gameObject.SetActive(active);
			}
		}

		public InsertNoticeRoot()
		{
		}
	}
}
