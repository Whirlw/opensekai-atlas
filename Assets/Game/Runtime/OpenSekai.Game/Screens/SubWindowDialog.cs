using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sekai.UI;
using UnityEngine;

namespace Sekai
{
	public class SubWindowDialog : DialogBase
	{
		[SerializeField]
		protected CustomTextMesh messageBodyText;

		[SerializeField]
		protected SubWindowComponent subWindowComponent;

		[SerializeField]
		protected SubWindowAnimationBase animation;

		private Action _onClose;
		private bool _isDisplayed;

		public string MessageBody { get; private set; }
		public override bool EnableBlur => false;

		public void Setup(string messageBody, Action _onClose)
		{
			MessageBody = messageBody;
			this._onClose = _onClose;
			SetMessageBodyText(messageBody);
			subWindowComponent?.Setup(CloseProcess);
		}

		public virtual void Initialize(string messageBody, Action _onClose, bool allowCloseExternal)
		{
			base.Initialize(DialogSize.Manual, allowCloseExternal);
			SetMessageBodyText(messageBody);
			if (subWindowComponent != null)
			{
				subWindowComponent.SetCallback(CloseProcess);
				subWindowComponent.SetBackPanel();
			}
			this._onClose = _onClose;
		}

		protected virtual void SetMessageBodyText(string messageBody)
		{
			MessageBody = messageBody;
			if (messageBodyText != null)
			{
				messageBodyText.SetText(messageBody ?? string.Empty);
			}
		}

		protected override void InitializeAnimation()
		{
			animation?.InitializeAnimation();
			if (fillCover != null)
			{
				fillCover.FillColor = ColorUtility.BLACK_ALPHA_0;
			}
		}

		protected override void OpenAnimation()
		{
			OpenAnimationAsync().Forget();
		}

		private async UniTask OpenAnimationAsync()
		{
			await ShowAsync();
			OnFinishOpenAnimation();
		}

		protected override void CloseAnimation()
		{
			CloseAnimationAsync(gameObject.GetCancellationTokenOnDestroy()).Forget();
		}

		private async UniTask CloseAnimationAsync(CancellationToken ct)
		{
			await HideAsync(ct);
			ct.ThrowIfCancellationRequested();
			Destroy();
		}

		public virtual void Show()
		{
			ShowAsync().Forget();
		}

		public virtual async UniTask ShowAsync()
		{
			SetDisplayed(true);
			if (animation != null)
			{
				await animation.OpenAnimation();
			}
		}

		public virtual void Hide()
		{
			HideAsync(gameObject.GetCancellationTokenOnDestroy()).Forget();
		}

		public virtual async UniTask HideAsync(CancellationToken ct = default)
		{
			SetDisplayed(false);
			if (animation != null)
			{
				await animation.CloseAnimation(ct);
			}
		}

		public virtual void HideImmediately()
		{
			SetDisplayed(false);
			animation?.CloseImmediately();
		}

		protected virtual void SetDisplayed(bool value)
		{
			_isDisplayed = value;
			SetCanvasGroup(value);
		}

		protected virtual void SetCanvasGroup(bool isActive)
		{
			if (canvasGroup == null)
			{
				canvasGroup = GetComponent<CanvasGroup>();
				if (canvasGroup == null)
				{
					canvasGroup = gameObject.AddComponent<CanvasGroup>();
				}
			}
			canvasGroup.interactable = isActive;
			canvasGroup.blocksRaycasts = isActive;
		}

		protected override void OnCloseExternal()
		{
			CloseProcess();
		}

		protected override void OnHardwareBackKeyProcess()
		{
			CloseProcess();
		}

		public virtual void CloseProcess()
		{
			Close();
		}

		public override void Close()
		{
			_onClose?.Invoke();
			_onClose = null;
			base.Close();
		}
	}
}
