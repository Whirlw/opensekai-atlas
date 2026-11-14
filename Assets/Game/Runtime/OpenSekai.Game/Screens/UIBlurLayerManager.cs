using System.Threading;
using Cysharp.Threading.Tasks;
using Sekai.Rendering;
using Sekai.UI;
using UnityEngine;
using Sekai.Core.Live;

namespace Sekai
{
	public class UIBlurLayerManager : MonoBehaviour
	{
		public enum Mode
		{
			None = -1,
			OnShot = 0,
			Always = 1
		}

		[SerializeField]
		private Canvas _canvas;

		[SerializeField]
		private CustomImage _image;

		private CancellationToken _cancellationToken;
		private Mode _captureMode = Mode.OnShot;

		public Mode CaptureMode
		{
			get => _captureMode;
			private set
			{
				if (_captureMode == value)
				{
					return;
				}

				_captureMode = value;
				if (_image != null && _image.enabled)
				{
					Enable();
				}
			}
		}

		public bool BlurExecuting => SekaiUIEffectSettings.Blur.IsActive;
		public bool IsEnabledBlurImage => _image != null && _image.enabled && _image.gameObject.activeInHierarchy;

		private void Awake()
		{
			InitializeReferences();
			_cancellationToken = gameObject.GetCancellationTokenOnDestroy();
			if (_image != null)
			{
				_image.enabled = false;
				_image.raycastTarget = false;
			}
		}

		public void SetBaseCanvas(Canvas baseCanvas)
		{
			SetCanvas(baseCanvas);
		}

		public void SetCanvas(Canvas baseCanvas)
		{
			InitializeReferences();
			if (_canvas == null || baseCanvas == null)
			{
				return;
			}

			_canvas.renderMode = baseCanvas.renderMode;
			_canvas.worldCamera = baseCanvas.worldCamera;
			_canvas.planeDistance = baseCanvas.planeDistance;
			_canvas.pixelPerfect = baseCanvas.pixelPerfect;
			_canvas.overrideSorting = baseCanvas.overrideSorting;
			_canvas.overridePixelPerfect = baseCanvas.overridePixelPerfect;
			_canvas.sortingLayerID = baseCanvas.sortingLayerID;
			_canvas.sortingOrder = baseCanvas.sortingOrder;
			_canvas.targetDisplay = baseCanvas.targetDisplay;
		}

		public UniTask SetCaptureMode(Mode mode)
		{
			if (mode == Mode.None)
			{
				_captureMode = mode;
				Disable();
				return UniTask.CompletedTask;
			}

			CaptureMode = mode;
			return UniTask.CompletedTask;
		}

		public void Enable()
		{
			InitializeReferences();
			if (CaptureMode == Mode.None)
			{
				return;
			}

			StartBlurCaptureAsync(SekaiUIEffectSettings.Blur, _cancellationToken).Forget();
		}

		public void Disable()
		{
			SekaiUIEffectSettings.Blur.IsActive = false;
			if (_image != null)
			{
				_image.enabled = false;
			}
		}

		private async UniTaskVoid StartBlurCaptureAsync(SekaiUIBlurParameter blur, CancellationToken token)
		{
			token.ThrowIfCancellationRequested();
			if (_image != null)
			{
				_image.enabled = true;
				_image.raycastTarget = false;
			}

			blur.IsActive = true;
			if (CaptureMode != Mode.OnShot)
			{
				return;
			}

			await UniTask.WaitForEndOfFrame(this, token);
			blur.IsActive = false;
		}

		private void InitializeReferences()
		{
			if (_canvas == null)
			{
				_canvas = GetComponent<Canvas>();
			}
			if (_image == null)
			{
				_image = GetComponentInChildren<CustomImage>(true);
			}
		}
	}
}
