using System.Reflection;
using NUnit.Framework;
using Sekai;
using Sekai.Live;
using UnityEngine;

namespace OpenSekai.Tests.EditMode.Live
{
	/// <summary>
	/// The AUTO popup is one sprite driven by the frame's worst judgement. It used to restart its
	/// animation on every judgement, which in a dense stretch (a note every few frames) means the
	/// clock never ran: the popup sat at zero scale and looked dead. One pop at a time now.
	/// </summary>
	public sealed class JudgmentPopupTests
	{
		private const float PopSeconds = 0.3f;

		private static readonly FieldInfo TimeField =
			typeof(JudgmentView).GetField("_time", BindingFlags.NonPublic | BindingFlags.Instance);

		private static readonly FieldInfo RendererField =
			typeof(JudgmentView).GetField("_spriteRenderer", BindingFlags.NonPublic | BindingFlags.Instance);

		private GameObject _host;
		private JudgmentView _view;
		private SpriteRenderer _renderer;
		private Sprite _autoSprite;
		private Sprite _missSprite;

		[SetUp]
		public void SetUp()
		{
			_host = new GameObject("judgment-view-test");
			_renderer = _host.AddComponent<SpriteRenderer>();
			_view = _host.AddComponent<JudgmentView>();
			RendererField.SetValue(_view, _renderer);
			_autoSprite = MakeSprite("auto");
			_missSprite = MakeSprite("miss");
			SetSprite("_auto", _autoSprite);
			SetSprite("_miss", _missSprite);
		}

		[TearDown]
		public void TearDown()
		{
			Object.DestroyImmediate(_autoSprite.texture);
			Object.DestroyImmediate(_missSprite.texture);
			Object.DestroyImmediate(_host);
		}

		private static Sprite MakeSprite(string name)
		{
			var texture = new Texture2D(1, 1) { name = name };
			return Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), Vector2.zero);
		}

		private void SetSprite(string field, Sprite sprite)
		{
			typeof(JudgmentView).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)
				?.SetValue(_view, sprite);
		}

		[Test]
		public void AJudgementInTheMiddleOfAPopDoesNotRestartIt()
		{
			_view.Excute((NoteResult.Auto, NoteResultDescription.None));
			TimeField.SetValue(_view, 0.1f);

			_view.Excute((NoteResult.Auto, NoteResultDescription.None));

			Assert.That(TimeField.GetValue(_view), Is.EqualTo(0.1f).Within(0.0001f),
				"a pop that is still playing must be allowed to finish");
			Assert.That(_renderer.sprite, Is.SameAs(_autoSprite), "and it still shows the newest result");
		}

		[Test]
		public void TheNextJudgementAfterAPopRestartsIt()
		{
			TimeField.SetValue(_view, PopSeconds + 0.05f);

			_view.Excute((NoteResult.Auto, NoteResultDescription.None));

			Assert.That(TimeField.GetValue(_view), Is.EqualTo(0f),
				"once the pop is over the next judgement starts a new one");
		}

		[Test]
		public void AWorseResultRepaintsAtOnce()
		{
			TimeField.SetValue(_view, 0.1f);
			_view.Excute((NoteResult.Auto, NoteResultDescription.None));

			TimeField.SetValue(_view, 0.1f);
			_view.Excute((NoteResult.Miss, NoteResultDescription.None));

			Assert.That(_renderer.sprite, Is.SameAs(_missSprite));
			Assert.That(TimeField.GetValue(_view), Is.EqualTo(0f), "a worse result takes the popup over");
		}
	}
}
