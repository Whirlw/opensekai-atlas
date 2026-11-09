using System;
using System.Runtime.CompilerServices;
using Beebyte.Obfuscator;
using Sekai.UI;
using UnityEngine;

namespace Sekai
{
	public class FriendInviteNoticeCell : MonoBehaviour
	{
		[SerializeField]
		private GameObject _requestRoot;

		[SerializeField]
		private GameObject _rejectRoot;

		[SerializeField]
		private GameObject _approveRoot;

		[SerializeField]
		private CustomTextMesh _requestTitleText;

		[SerializeField]
		private CustomTextMesh _requestMessageText;

		[SerializeField]
		private CustomButton _requestRejectButton;

		[SerializeField]
		private CustomButton _requestApproveButton;

		[SerializeField]
		private CustomImage _symbolImage;

		public Action OnRequestApprove
		{
			[CompilerGenerated]
			get
			{
				throw null;
			}
			[CompilerGenerated]
			set
			{
				throw null;
			}
		}

		public Action OnRequestReject
		{
			[CompilerGenerated]
			get
			{
				throw null;
			}
			[CompilerGenerated]
			set
			{
				throw null;
			}
		}

		public string InvitationId
		{
			[CompilerGenerated]
			get
			{
				throw null;
			}
			[CompilerGenerated]
			private set
			{
				throw null;
			}
		}

		public InvitationType InvitationType
		{
			[CompilerGenerated]
			get
			{
				throw null;
			}
			[CompilerGenerated]
			private set
			{
				throw null;
			}
		}

		public string RoomId
		{
			[CompilerGenerated]
			get
			{
				throw null;
			}
			[CompilerGenerated]
			private set
			{
				throw null;
			}
		}

		[Skip]
		public void Refresh(ReceiveFriendInviteViewData inviteViewData, string message)
		{
			throw null;
		}

		public void SetEnableButton(bool enable)
		{
			throw null;
		}

		public void EnableRejectContent()
		{
			throw null;
		}

		public void EnableApproveContent()
		{
			throw null;
		}

		private void Awake()
		{
			throw null;
		}

		private void OnDestroy()
		{
			throw null;
		}

		public FriendInviteNoticeCell()
		{
		}
	}
}
