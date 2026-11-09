using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai
{
	public class ColorFader : MonoBehaviour
	{
		public enum State
		{
			None = 0,
			Fading = 1
		}

		public enum Style
		{
			Once = 0,
			Loop = 1,
			PingPong = 2
		}

		public enum BlendType
		{
			Direct = 0,
			Mul = 1
		}

		public interface ITargetColorObj
		{
			Color TargetColor { get; set; }

			void Init(GameObject targetObj, bool containChildren);
		}

		public class GraphicColor : ITargetColorObj
		{
			private List<Graphic> _sprites;

			private bool _includeChildren;

			public Color TargetColor
			{
				get
				{
					if (_sprites == null || _sprites.Count == 0)
					{
						return Color.white;
					}

					return _sprites[0].color;
				}
				set
				{
					if (_sprites == null)
					{
						return;
					}

					foreach (var graphic in _sprites)
					{
						if (graphic != null)
						{
							graphic.color = value;
						}
					}
				}
			}

			public void Init(GameObject targetObj, bool containChildren)
			{
				_sprites = new List<Graphic>();
				_includeChildren = containChildren;
				if (targetObj != null)
				{
					CollectTargets(targetObj.transform);
				}
			}

			private void CollectTargets(Transform targetTrans)
			{
				if (targetTrans == null)
				{
					return;
				}

				var graphic = targetTrans.GetComponent<Graphic>();
				if (graphic != null)
				{
					_sprites.Add(graphic);
				}

				if (!_includeChildren)
				{
					return;
				}

				for (var i = 0; i < targetTrans.childCount; i++)
				{
					CollectTargets(targetTrans.GetChild(i));
				}
			}

			public GraphicColor()
			{
				_sprites = new List<Graphic>();
			}
		}

		public class SpriteRendererColor : ITargetColorObj
		{
			private List<SpriteRenderer> _sprites;

			private bool _includeChildren;

			public Color TargetColor
			{
				get
				{
					if (_sprites == null || _sprites.Count == 0)
					{
						return Color.white;
					}

					return _sprites[0].color;
				}
				set
				{
					if (_sprites == null)
					{
						return;
					}

					foreach (var sprite in _sprites)
					{
						if (sprite != null)
						{
							sprite.color = value;
						}
					}
				}
			}

			public void Init(GameObject targetObj, bool containChildren)
			{
				_sprites = new List<SpriteRenderer>();
				_includeChildren = containChildren;
				if (targetObj != null)
				{
					CollectTargets(targetObj.transform);
				}
			}

			private void CollectTargets(Transform targetTrans)
			{
				if (targetTrans == null)
				{
					return;
				}

				var sprite = targetTrans.GetComponent<SpriteRenderer>();
				if (sprite != null)
				{
					_sprites.Add(sprite);
				}

				if (!_includeChildren)
				{
					return;
				}

				for (var i = 0; i < targetTrans.childCount; i++)
				{
					CollectTargets(targetTrans.GetChild(i));
				}
			}

			public SpriteRendererColor()
			{
				_sprites = new List<SpriteRenderer>();
			}
		}

		public class AreaSpriteRendererMultiplicationColor : ITargetColorObj
		{
			private List<AreaSpriteRenderer> _sprites;

			private bool _includeChildren;

			public Color TargetColor
			{
				get
				{
					if (_sprites == null || _sprites.Count == 0)
					{
						return Color.white;
					}

					return _sprites[0].MultiplicationColor;
				}
				set
				{
					if (_sprites == null)
					{
						return;
					}

					foreach (var sprite in _sprites)
					{
						if (sprite != null)
						{
							sprite.MultiplicationColor = value;
						}
					}
				}
			}

			public void Init(GameObject targetObj, bool containChildren)
			{
				_sprites = new List<AreaSpriteRenderer>();
				_includeChildren = containChildren;
				if (targetObj != null)
				{
					CollectTargets(targetObj.transform);
				}
			}

			private void CollectTargets(Transform targetTrans)
			{
				if (targetTrans == null)
				{
					return;
				}

				var sprite = targetTrans.GetComponent<AreaSpriteRenderer>();
				if (sprite != null)
				{
					_sprites.Add(sprite);
				}

				if (!_includeChildren)
				{
					return;
				}

				for (var i = 0; i < targetTrans.childCount; i++)
				{
					CollectTargets(targetTrans.GetChild(i));
				}
			}

			public AreaSpriteRendererMultiplicationColor()
			{
				_sprites = new List<AreaSpriteRenderer>();
			}
		}

		public class AreaSpriteRendererColor : ITargetColorObj
		{
			private List<AreaSpriteRenderer> _sprites;

			private bool _includeChildren;

			public Color TargetColor
			{
				get
				{
					if (_sprites == null || _sprites.Count == 0)
					{
						return Color.white;
					}

					return _sprites[0].BaseColor;
				}
				set
				{
					if (_sprites == null)
					{
						return;
					}

					foreach (var sprite in _sprites)
					{
						if (sprite != null)
						{
							sprite.BaseColor = value;
						}
					}
				}
			}

			public void Init(GameObject targetObj, bool containChildren)
			{
				_sprites = new List<AreaSpriteRenderer>();
				_includeChildren = containChildren;
				if (targetObj != null)
				{
					CollectTargets(targetObj.transform);
				}
			}

			private void CollectTargets(Transform targetTrans)
			{
				if (targetTrans == null)
				{
					return;
				}

				var sprite = targetTrans.GetComponent<AreaSpriteRenderer>();
				if (sprite != null)
				{
					_sprites.Add(sprite);
				}

				if (!_includeChildren)
				{
					return;
				}

				for (var i = 0; i < targetTrans.childCount; i++)
				{
					CollectTargets(targetTrans.GetChild(i));
				}
			}

			public AreaSpriteRendererColor()
			{
				_sprites = new List<AreaSpriteRenderer>();
			}
		}

		public class SpineModelColor : ITargetColorObj
		{
			private List<SpineModel> _sprites;

			private bool _includeChildren;

			public Color TargetColor
			{
				get
				{
					if (_sprites == null || _sprites.Count == 0)
					{
						return Color.white;
					}

					return _sprites[0].BaseColor;
				}
				set
				{
					if (_sprites == null)
					{
						return;
					}

					foreach (var sprite in _sprites)
					{
						if (sprite != null)
						{
							sprite.BaseColor = value;
						}
					}
				}
			}

			public void Init(GameObject targetObj, bool containChildren)
			{
				_sprites = new List<SpineModel>();
				_includeChildren = containChildren;
				if (targetObj != null)
				{
					CollectTargets(targetObj.transform);
				}
			}

			private void CollectTargets(Transform targetTrans)
			{
				if (targetTrans == null)
				{
					return;
				}

				var sprite = targetTrans.GetComponent<SpineModel>();
				if (sprite != null)
				{
					_sprites.Add(sprite);
				}

				if (!_includeChildren)
				{
					return;
				}

				for (var i = 0; i < targetTrans.childCount; i++)
				{
					CollectTargets(targetTrans.GetChild(i));
				}
			}

			public SpineModelColor()
			{
				_sprites = new List<SpineModel>();
			}
		}

		public class SpineModelMultiplicationColor : ITargetColorObj
		{
			private List<SpineModel> _sprites;

			private bool _includeChildren;

			public Color TargetColor
			{
				get
				{
					if (_sprites == null || _sprites.Count == 0)
					{
						return Color.white;
					}

					return _sprites[0].MultiplicationColor;
				}
				set
				{
					if (_sprites == null)
					{
						return;
					}

					foreach (var sprite in _sprites)
					{
						if (sprite != null)
						{
							sprite.MultiplicationColor = value;
						}
					}
				}
			}

			public void Init(GameObject targetObj, bool containChildren)
			{
				_sprites = new List<SpineModel>();
				_includeChildren = containChildren;
				if (targetObj != null)
				{
					CollectTargets(targetObj.transform);
				}
			}

			private void CollectTargets(Transform targetTrans)
			{
				if (targetTrans == null)
				{
					return;
				}

				var sprite = targetTrans.GetComponent<SpineModel>();
				if (sprite != null)
				{
					_sprites.Add(sprite);
				}

				if (!_includeChildren)
				{
					return;
				}

				for (var i = 0; i < targetTrans.childCount; i++)
				{
					CollectTargets(targetTrans.GetChild(i));
				}
			}

			public SpineModelMultiplicationColor()
			{
				_sprites = new List<SpineModel>();
			}
		}

		[CompilerGenerated]
		private sealed class _003CPlayChainFade_003Ed__64 : IEnumerator<object>, IEnumerator, IDisposable
		{
			private int _003C_003E1__state;

			private object _003C_003E2__current;

			public ColorFader _003C_003E4__this;

			public Action onFinish;

			public Color[] targetColors;

			public float delay;

			public float oneFadeDuration;

			private Color _003CbaseColor_003E5__2;

			private Color _003CtargetColor_003E5__3;

			private float _003Ctime_003E5__4;

			private int _003CtableLength_003E5__5;

			private int _003CindexVec_003E5__6;

			private bool _003CisPlayable_003E5__7;

			private int _003Ci_003E5__8;

			object IEnumerator<object>.Current
			{
				[DebuggerHidden]
				get
				{
					throw null;
				}
			}

			object IEnumerator.Current
			{
				[DebuggerHidden]
				get
				{
					throw null;
				}
			}

			[DebuggerHidden]
			public _003CPlayChainFade_003Ed__64(int _003C_003E1__state)
			{
				throw null;
			}

			[DebuggerHidden]
			void IDisposable.Dispose()
			{
				throw null;
			}

			private bool MoveNext()
			{
				throw null;
			}

			bool IEnumerator.MoveNext()
			{
				//ILSpy generated this explicit interface implementation from .override directive in MoveNext
				return this.MoveNext();
			}

			[DebuggerHidden]
			void IEnumerator.Reset()
			{
				throw null;
			}
		}

		[CompilerGenerated]
		private sealed class _003CPlaySimpleAlphaFade_003Ed__63 : IEnumerator<object>, IEnumerator, IDisposable
		{
			private int _003C_003E1__state;

			private object _003C_003E2__current;

			public ColorFader _003C_003E4__this;

			public Action onFinish;

			public float delay;

			public float targetAlpha;

			public float duration;

			private Color _003CbaseColor_003E5__2;

			private float _003Ctime_003E5__3;

			object IEnumerator<object>.Current
			{
				[DebuggerHidden]
				get
				{
					throw null;
				}
			}

			object IEnumerator.Current
			{
				[DebuggerHidden]
				get
				{
					throw null;
				}
			}

			[DebuggerHidden]
			public _003CPlaySimpleAlphaFade_003Ed__63(int _003C_003E1__state)
			{
				throw null;
			}

			[DebuggerHidden]
			void IDisposable.Dispose()
			{
				throw null;
			}

			private bool MoveNext()
			{
				throw null;
			}

			bool IEnumerator.MoveNext()
			{
				//ILSpy generated this explicit interface implementation from .override directive in MoveNext
				return this.MoveNext();
			}

			[DebuggerHidden]
			void IEnumerator.Reset()
			{
				throw null;
			}
		}

		[CompilerGenerated]
		private sealed class _003CPlaySimpleFade_003Ed__62 : IEnumerator<object>, IEnumerator, IDisposable
		{
			private int _003C_003E1__state;

			private object _003C_003E2__current;

			public ColorFader _003C_003E4__this;

			public Action onFinish;

			public float delay;

			public Color targetColor;

			public float duration;

			private Color _003CbaseColor_003E5__2;

			private float _003Ctime_003E5__3;

			object IEnumerator<object>.Current
			{
				[DebuggerHidden]
				get
				{
					throw null;
				}
			}

			object IEnumerator.Current
			{
				[DebuggerHidden]
				get
				{
					throw null;
				}
			}

			[DebuggerHidden]
			public _003CPlaySimpleFade_003Ed__62(int _003C_003E1__state)
			{
				throw null;
			}

			[DebuggerHidden]
			void IDisposable.Dispose()
			{
				throw null;
			}

			private bool MoveNext()
			{
				throw null;
			}

			bool IEnumerator.MoveNext()
			{
				//ILSpy generated this explicit interface implementation from .override directive in MoveNext
				return this.MoveNext();
			}

			[DebuggerHidden]
			void IEnumerator.Reset()
			{
				throw null;
			}
		}

		[SerializeField]
		private GameObject _targetGameObject;

		[SerializeField]
		private BlendType _blendType;

		[SerializeField]
		private Color _defaultColor;

		[SerializeField]
		private Color[] _defaultColorTable;

		[SerializeField]
		private Style _defaultStyle;

		[SerializeField]
		private float _defaultDelay;

		[SerializeField]
		private float _defaultDuration;

		[SerializeField]
		private bool _playOnAwake;

		[SerializeField]
		private bool _playOnEnable;

		[SerializeField]
		private bool _includeChildren;

		private ITargetColorObj _targetSprite;

		private Color _fadeTargetColor;

		private IEnumerator _fadeCoroutine;

		private bool _isAlphaZeroDeactive;

		private int _currentColorTableIndex;

		private Style _style;

		private State _state;

		private MonoBehaviour _coroutineExecutor;

		private Action _onFinishedNotExecuted;

		public bool IsAlphaZeroDeactive
		{
			[CompilerGenerated]
			get
			{
				return _isAlphaZeroDeactive;
			}
			[CompilerGenerated]
			set
			{
				_isAlphaZeroDeactive = value;
			}
		}

		public Color[] DefaultColorTable
		{
			get
			{
				return _defaultColorTable;
			}
			set
			{
				_defaultColorTable = value;
			}
		}

		public float DefaultDuration
		{
			get
			{
				return _defaultDuration;
			}
			set
			{
				_defaultDuration = value;
			}
		}

		public Color FadeTargetColor
		{
			get
			{
				return _fadeTargetColor;
			}
		}

		public bool IsPlayFading
		{
			get
			{
				return _state == State.Fading;
			}
		}

		public State CurrentState
		{
			get
			{
				return _state;
			}
		}

		public Color CurrentColor
		{
			get
			{
				return _targetSprite != null ? _targetSprite.TargetColor : _defaultColor;
			}
			set
			{
				if (_targetSprite != null)
				{
					_targetSprite.TargetColor = value;
				}
			}
		}

		public Color DefaultColor
		{
			get
			{
				return _defaultColor;
			}
			set
			{
				_defaultColor = value;
			}
		}

		public bool IncludeChildren
		{
			get
			{
				return _includeChildren;
			}
			set
			{
				_includeChildren = value;
			}
		}

		public bool IsFoundFadeTarget
		{
			get
			{
				return _targetSprite != null;
			}
		}

		private void Awake()
		{
			if (_playOnAwake)
			{
				Initialize();
				PlayColorTable(null);
			}
		}

		public bool Initialize(bool isForce = false)
		{
			if (!isForce && _targetSprite != null)
			{
				return true;
			}

			return FindFadeTarget(isForce);
		}

		public void Rebuild(bool isForce = false)
		{
			Initialize(isForce);
		}

		private void OnEnable()
		{
			if (_playOnEnable)
			{
				Initialize();
				PlayColorTable(null);
			}
		}

		public bool FindFadeTarget(bool isForce = false)
		{
			if (!isForce && _targetSprite != null)
			{
				return true;
			}

			_targetSprite = FindSpriteType();
			_targetSprite?.Init(_targetGameObject != null ? _targetGameObject : gameObject, _includeChildren);
			return _targetSprite != null;
		}

		private ITargetColorObj FindSpriteType()
		{
			var target = _targetGameObject != null ? _targetGameObject : gameObject;
			if (target == null)
			{
				return null;
			}

			if (target.GetComponent<Graphic>() != null || target.GetComponentInChildren<Graphic>(true) != null)
			{
				return new GraphicColor();
			}

			if (target.GetComponent<SpriteRenderer>() != null || target.GetComponentInChildren<SpriteRenderer>(true) != null)
			{
				return new SpriteRendererColor();
			}

			return null;
		}

		public void Set(Color targetColor)
		{
			Initialize();
			Stop();
			_fadeTargetColor = targetColor;
			CurrentColor = targetColor;
			if (_isAlphaZeroDeactive || IsAlphaZeroDeactive)
			{
				if (targetColor.a <= 0f)
				{
					DeactiveSprite();
				}
				else
				{
					ActiveSprite();
				}
			}
		}

		public void Play(Color targetColor, float delay, float duration, Action onFinish = null)
		{
			Initialize();
			Stop();
			_fadeTargetColor = targetColor;
			_fadeCoroutine = PlaySimpleFade(targetColor, delay, duration, onFinish);
			(_coroutineExecutor != null ? _coroutineExecutor : this).StartCoroutine(_fadeCoroutine);
		}

		public void PlayAlpha(float targetAlpha, float delay, float duration, Action onFinish = null)
		{
			Initialize();
			var color = CurrentColor;
			color.a = targetAlpha;
			Play(color, delay, duration, onFinish);
		}

		public void PlayColorTable(Color[] targetColors, float delay, float oneFadeDuration, Style _style, Action onFinish = null)
		{
			_defaultColorTable = targetColors;
			_defaultDelay = delay;
			_defaultDuration = oneFadeDuration;
			this._style = _style;
			Stop();
			_fadeCoroutine = PlayChainFade(targetColors, delay, oneFadeDuration, onFinish);
			(_coroutineExecutor != null ? _coroutineExecutor : this).StartCoroutine(_fadeCoroutine);
		}

		public void PlayColorTable(Action onFinish = null)
		{
			PlayColorTable(_defaultColorTable, _defaultDelay, _defaultDuration, _defaultStyle, onFinish);
		}

		public void Stop()
		{
			if (_fadeCoroutine != null)
			{
				(_coroutineExecutor != null ? _coroutineExecutor : this).StopCoroutine(_fadeCoroutine);
				_fadeCoroutine = null;
			}

			_state = State.None;
		}

		private IEnumerator PlaySimpleFade(Color targetColor, float delay, float duration, Action onFinish = null)
		{
			_state = State.Fading;
			if (delay > 0f)
			{
				yield return new WaitForSeconds(delay);
			}

			var startColor = CurrentColor;
			var elapsed = 0f;
			while (elapsed < duration)
			{
				elapsed += Time.deltaTime;
				var rate = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
				CurrentColor = Color.Lerp(startColor, targetColor, rate);
				yield return null;
			}

			_fadeTargetColor = targetColor;
			CurrentColor = targetColor;
			_state = State.None;
			onFinish?.Invoke();
		}

		private IEnumerator PlaySimpleAlphaFade(float targetAlpha, float delay, float duration, Action onFinish = null)
		{
			var color = CurrentColor;
			color.a = targetAlpha;
			yield return PlaySimpleFade(color, delay, duration, onFinish);
		}

		private IEnumerator PlayChainFade(Color[] targetColors, float delay, float oneFadeDuration, Action onFinish = null)
		{
			if (targetColors == null || targetColors.Length == 0)
			{
				onFinish?.Invoke();
				yield break;
			}

			if (delay > 0f)
			{
				yield return new WaitForSeconds(delay);
			}

			_state = State.Fading;
			_currentColorTableIndex = 0;
			do
			{
				yield return PlaySimpleFade(targetColors[_currentColorTableIndex], 0f, oneFadeDuration, null);
				_currentColorTableIndex++;
				if (_currentColorTableIndex >= targetColors.Length)
				{
					if (_style == Style.Loop)
					{
						_currentColorTableIndex = 0;
					}
					else
					{
						break;
					}
				}
			}
			while (_style == Style.Loop);

			_state = State.None;
			onFinish?.Invoke();
		}

		private void DeactiveSprite()
		{
			var target = _targetGameObject != null ? _targetGameObject : gameObject;
			if (target != null)
			{
				target.SetActive(false);
			}
		}

		private void ActiveSprite()
		{
			var target = _targetGameObject != null ? _targetGameObject : gameObject;
			if (target != null)
			{
				target.SetActive(true);
			}
		}

		public ColorFader()
		{
			_defaultColor = Color.white;
			_defaultDuration = 0.2f;
			_defaultStyle = Style.Once;
			_state = State.None;
		}
	}
}
