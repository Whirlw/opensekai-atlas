using System;

namespace OpenSekai.Shared.Live.Simulation
{
	/// <summary>
	/// Where a live starts and ends on the audio clock, and where its skip buttons send it.
	///
	/// Four times are read when the chart loads, all in audio seconds: when the music starts and ends,
	/// and when the first and last note fall. The live runs over the union of the two spans: it starts at
	/// whichever comes first and ends at whichever comes last, so a chart that runs past the end of its
	/// audio keeps its clock going until the last note, and a chart that starts before its audio gets a
	/// silent run-in instead of notes that appear already on the line.
	///
	/// The first note has to be on screen before it falls, so the chart side of the start is the first
	/// note minus <see cref="LeadInSeconds"/>, the time a note takes to scroll down.
	///
	/// Both ends get a breath: whichever of the music and the first note comes first lands
	/// <see cref="RunInSeconds"/> after the info page is gone, and the result effect comes
	/// <see cref="RunOutSeconds"/> after whichever of the music and the last note ends last.
	///
	/// The intro skip is available from the playable start until <see cref="RunInSeconds"/>
	/// before the first note, and jumps to that point. The outro skip is available
	/// <see cref="OutroSkipDelaySeconds"/> after the last note and plays the result effect immediately.
	/// </summary>
	public readonly struct LiveTimelineSpan
	{
		/// <summary>Time after the last note before the outro skip is available.</summary>
		public const float OutroSkipDelaySeconds = 1f;

		/// <summary>From the info page going away to the first of the music and the first note.</summary>
		public const float RunInSeconds = 2f;

		/// <summary>From the last of the music and the last note to the result effect.</summary>
		public const float RunOutSeconds = 1f;

		/// <summary>A span whose audio plays exactly over its audible part (no silence at either end).</summary>
		public LiveTimelineSpan(float musicStartSeconds, float musicEndSeconds, float firstNoteSeconds, float lastNoteSeconds, float leadInSeconds)
			: this(musicStartSeconds, musicEndSeconds, firstNoteSeconds, lastNoteSeconds, leadInSeconds, musicStartSeconds, musicEndSeconds)
		{
		}

		/// <param name="musicStartSeconds">Where the music becomes audible. Decides the skips only.</param>
		/// <param name="musicEndSeconds">Where the music stops being audible. Decides the skips only.</param>
		/// <param name="playbackStartSeconds">Where the audio file starts playing; its silence is played too.</param>
		/// <param name="playbackEndSeconds">Where the audio file ends.</param>
		public LiveTimelineSpan(float musicStartSeconds, float musicEndSeconds, float firstNoteSeconds, float lastNoteSeconds, float leadInSeconds,
			float playbackStartSeconds, float playbackEndSeconds)
		{
			MusicStartSeconds = musicStartSeconds;
			MusicEndSeconds = Math.Max(musicStartSeconds, musicEndSeconds);
			PlaybackStartSeconds = Math.Min(playbackStartSeconds, MusicStartSeconds);
			PlaybackEndSeconds = Math.Max(playbackEndSeconds, MusicEndSeconds);
			HasNotes = !float.IsNaN(firstNoteSeconds) && !float.IsNaN(lastNoteSeconds);
			FirstNoteSeconds = HasNotes ? Math.Min(firstNoteSeconds, lastNoteSeconds) : float.NaN;
			LastNoteSeconds = HasNotes ? Math.Max(firstNoteSeconds, lastNoteSeconds) : float.NaN;
			LeadInSeconds = Math.Max(0f, leadInSeconds);
		}

		/// <summary>The audible start of the music: what the skips are measured from.</summary>
		public float MusicStartSeconds { get; }

		/// <summary>The audible end of the music: what the skips are measured to.</summary>
		public float MusicEndSeconds { get; }

		/// <summary>Where the audio file starts playing, silence included.</summary>
		public float PlaybackStartSeconds { get; }

		/// <summary>Where the audio file stops, silence included.</summary>
		public float PlaybackEndSeconds { get; }
		public float FirstNoteSeconds { get; }
		public float LastNoteSeconds { get; }
		public float LeadInSeconds { get; }
		public bool HasNotes { get; }

		/// <summary>When the first note has to start scrolling in.</summary>
		public float ChartStartSeconds => HasNotes ? FirstNoteSeconds - LeadInSeconds : MusicStartSeconds;

		/// <summary>
		/// The start point, which every live reaches the moment its info page is gone:
		/// <see cref="RunInSeconds"/> before the audible music or the first note, whichever is first.
		/// A lead-in longer than that moves it earlier still, so the first note always scrolls in whole.
		/// The silence before it is not cut: the file is played from earlier on, so that this point
		/// lands where it should (see <see cref="ClockAtIntroStart"/>).
		/// </summary>
		public float StartSeconds => HasNotes
			? Math.Min(Math.Min(MusicStartSeconds, FirstNoteSeconds) - RunInSeconds, ChartStartSeconds)
			: MusicStartSeconds - RunInSeconds;

		/// <summary>The end point: the audible music or the chart, whichever is last.</summary>
		public float EndSeconds => HasNotes ? Math.Max(MusicEndSeconds, LastNoteSeconds) : MusicEndSeconds;

		/// <summary>When the result effect plays, unless the outro skip brings it forward.</summary>
		public float ResultEffectSeconds => EndSeconds + RunOutSeconds;

		/// <summary>
		/// The clock when the info page goes up, given how long the page stays: the start point minus
		/// that time. Below <see cref="PlaybackStartSeconds"/> the file is still to come and the clock
		/// runs in on its own; above it the file has to begin part-way in, and only that part, which
		/// would have played before the audio was even loaded, is cut.
		/// </summary>
		public float ClockAtIntroStart(float introSeconds) => StartSeconds - Math.Max(0f, introSeconds);

		/// <summary>Where the intro skip lands, <see cref="RunInSeconds"/> before the first note.</summary>
		public float IntroSkipTargetSeconds =>
			HasNotes ? FirstNoteSeconds - RunInSeconds : MusicStartSeconds;

		public float OutroSkipAvailableSeconds => LastNoteSeconds + OutroSkipDelaySeconds;

		public bool HasIntroSkip => HasNotes && StartSeconds < IntroSkipTargetSeconds;

		public bool HasOutroSkip => HasNotes && OutroSkipAvailableSeconds < ResultEffectSeconds;

		public override string ToString() => string.Concat(
			"music=", MusicStartSeconds.ToString("F3"), "..", MusicEndSeconds.ToString("F3"),
			" file=", PlaybackStartSeconds.ToString("F3"), "..", PlaybackEndSeconds.ToString("F3"),
			" notes=", HasNotes ? FirstNoteSeconds.ToString("F3") + ".." + LastNoteSeconds.ToString("F3") : "none",
			" leadIn=", LeadInSeconds.ToString("F3"),
			" span=", StartSeconds.ToString("F3"), "..", EndSeconds.ToString("F3"),
			" result=", ResultEffectSeconds.ToString("F3"),
			" introSkip=", HasIntroSkip ? IntroSkipTargetSeconds.ToString("F3") : "no",
			" outroSkip=", HasOutroSkip ? "yes" : "no");
	}
}
