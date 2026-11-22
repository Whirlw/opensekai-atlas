using System;
using System.Collections;
using System.Collections.Generic;
using LitMotion;
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

		[UnityEngine.Serialization.FormerlySerializedAs("_targetGameObject")]
		[SerializeField]
		private GameObject targetGameObject;

		[UnityEngine.Serialization.FormerlySerializedAs("_blendType")]
		[SerializeField]
		private BlendType blendType;

		[UnityEngine.Serialization.FormerlySerializedAs("_defaultColor")]
		[SerializeField]
		private Color defaultColor;

		[UnityEngine.Serialization.FormerlySerializedAs("_defaultColorTable")]
		[SerializeField]
		private Color[] defaultColorTable;

		[UnityEngine.Serialization.FormerlySerializedAs("_defaultStyle")]
		[SerializeField]
		private Style defaultStyle;

		[UnityEngine.Serialization.FormerlySerializedAs("_defaultDelay")]
		[SerializeField]
		private float defaultDelay;

		[UnityEngine.Serialization.FormerlySerializedAs("_defaultDuration")]
		[SerializeField]
		private float defaultDuration;

		[UnityEngine.Serialization.FormerlySerializedAs("_playOnAwake")]
		[SerializeField]
		private bool playOnAwake;

		[UnityEngine.Serialization.FormerlySerializedAs("_playOnEnable")]
		[SerializeField]
		private bool playOnEnable;

		[UnityEngine.Serialization.FormerlySerializedAs("_includeChildren")]
		[SerializeField]
		private bool includeChildren;

		private ITargetColorObj _targetSprite;

		private Color _fadeTargetColor;

		private IEnumerator _fadeCoroutine;

		private bool _isAlphaZeroDeactive;

		private int _currentColorTableIndex;

		private Style _style;

		private State _state;

		private MotionHandle _fadeMotion;


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
				return defaultColorTable;
			}
			set
			{
				defaultColorTable = value;
			}
		}

		public float DefaultDuration
		{
			get
			{
				return defaultDuration;
			}
			set
			{
				defaultDuration = value;
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
				return _targetSprite != null ? _targetSprite.TargetColor : defaultColor;
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
				return defaultColor;
			}
			set
			{
				defaultColor = value;
			}
		}

		public bool IncludeChildren
		{
			get
			{
				return includeChildren;
			}
			set
			{
				includeChildren = value;
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
			if (playOnAwake)
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
			if (playOnEnable)
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
			_targetSprite?.Init(targetGameObject != null ? targetGameObject : gameObject, includeChildren);
			return _targetSprite != null;
		}

		private ITargetColorObj FindSpriteType()
		{
			var target = targetGameObject != null ? targetGameObject : gameObject;
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
			StartCoroutine(_fadeCoroutine);
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
			defaultColorTable = targetColors;
			defaultDelay = delay;
			defaultDuration = oneFadeDuration;
			this._style = _style;
			Stop();
			_fadeCoroutine = PlayChainFade(targetColors, delay, oneFadeDuration, onFinish);
			StartCoroutine(_fadeCoroutine);
		}

		public void PlayColorTable(Action onFinish = null)
		{
			PlayColorTable(defaultColorTable, defaultDelay, defaultDuration, defaultStyle, onFinish);
		}

		private void OnDisable() => Stop();

		public void Stop()
		{
			if (_fadeCoroutine != null)
			{
				StopCoroutine(_fadeCoroutine);
				_fadeCoroutine = null;
			}

			_fadeMotion.TryCancel();
			_state = State.None;
		}

		private IEnumerator PlaySimpleFade(Color targetColor, float delay, float duration, Action onFinish = null)
		{
			_state = State.Fading;
			if (delay > 0f)
			{
				yield return new WaitForSeconds(delay);
			}

			if (duration > 0f)
			{
				Color start = CurrentColor;
				_fadeMotion = LMotion.Create(0f, 1f, duration)
					.Bind(progress => CurrentColor = Color.LerpUnclamped(start, targetColor, progress));
				while (_fadeMotion.IsActive())
				{
					yield return null;
				}
			}

			_fadeTargetColor = targetColor;
			CurrentColor = targetColor;
			_state = State.None;
			onFinish?.Invoke();
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
			var target = targetGameObject != null ? targetGameObject : gameObject;
			if (target != null)
			{
				target.SetActive(false);
			}
		}

		private void ActiveSprite()
		{
			var target = targetGameObject != null ? targetGameObject : gameObject;
			if (target != null)
			{
				target.SetActive(true);
			}
		}

		public ColorFader()
		{
			defaultColor = Color.white;
			defaultDuration = 0.2f;
			defaultStyle = Style.Once;
			_state = State.None;
		}
	}
}
