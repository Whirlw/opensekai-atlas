using System;
using System.Runtime.CompilerServices;
using Sekai.UI;
using UnityEngine;

namespace Sekai
{
	public class FriendRequestNoticeCell : MonoBehaviour
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
		private UIPartsMarquee _requestTextMarquee;

		[SerializeField]
		private CustomButton _requestRejectButton;

		[SerializeField]
		private CustomButton _requestApproveButton;

				[SerializeField] private Action _onRequestApprove;
		public Action onRequestApprove
		{
		    get => _onRequestApprove;
		    set => _onRequestApprove = value;
		}

				[SerializeField] private Action _onRequestReject;
		public Action onRequestReject
		{
		    get => _onRequestReject;
		    set => _onRequestReject = value;
		}

		public UIPartsMarquee RequestTextMarquee
		{
			get
			{
				return _requestTextMarquee;
			}
		}

		public int RequestTextLength
		{
			get
			{
				return _requestMessageText != null && _requestMessageText.Text != null ? _requestMessageText.Text.Length : 0;
			}
		}

		public long UserId
		{
			[CompilerGenerated]
			get
			{
				return _UserId;
			}
			[CompilerGenerated]
			private set
			{
				_UserId = value;
			}
		}

		[CompilerGenerated]
		private long _UserId;

		public void Refresh(long userId, string requestTitle, string requestMessage)
		{
			if (_rejectRoot != null)
			{
				_rejectRoot.SetActive(false);
			}

			if (_approveRoot != null)
			{
				_approveRoot.SetActive(false);
			}

			if (_requestRoot != null)
			{
				_requestRoot.SetActive(true);
			}

			UserId = userId;
			_requestTitleText?.SetText(requestTitle);
			_requestMessageText?.SetText(requestMessage);
		}

		public void SetEnableButton(bool enable)
		{
			if (_requestRejectButton != null)
			{
				_requestRejectButton.enabled = enable;
			}

			if (_requestApproveButton != null)
			{
				_requestApproveButton.enabled = enable;
			}
		}

		public void EnableRejectContent()
		{
			if (_requestRoot != null)
			{
				_requestRoot.SetActive(false);
			}

			if (_rejectRoot != null)
			{
				_rejectRoot.SetActive(true);
			}
		}

		public void EnableApproveContent()
		{
			if (_requestRoot != null)
			{
				_requestRoot.SetActive(false);
			}

			if (_approveRoot != null)
			{
				_approveRoot.SetActive(true);
			}
		}

		private void Awake()
		{
			if (_requestRejectButton != null)
			{
				_requestRejectButton.onClick.AddListener(() => onRequestReject?.Invoke());
			}

			if (_requestApproveButton != null)
			{
				_requestApproveButton.onClick.AddListener(() => onRequestApprove?.Invoke());
			}
		}

		private void OnDestroy()
		{
			if (_requestApproveButton != null)
			{
				_requestApproveButton.onClick.RemoveAllListeners();
			}

			if (_requestRejectButton != null)
			{
				_requestRejectButton.onClick.RemoveAllListeners();
			}
		}

		public FriendRequestNoticeCell()
		{
		}
	}
}
