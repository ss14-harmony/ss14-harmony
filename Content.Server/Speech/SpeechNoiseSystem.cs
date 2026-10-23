using Content.Shared.Chat;
using Content.Shared.Speech;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Player; // Harmony - Use RMC14 to mute speechsounds
using Content.Shared._Box.Audio; // Box Change: Speech sound muting

namespace Content.Server.Speech
{
    public sealed partial class SpeechSoundSystem : EntitySystem
    {
        [Dependency] private IGameTiming _gameTiming = default!;
        [Dependency] private IRobustRandom _random = default!;
        [Dependency] private SharedAudioSystem _audio = default!;
        [Dependency] private readonly SpeciesMutingSystem _speciesMuting = default!; // Box Change: Speech sound muting
        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<SpeechComponent, EntitySpokeEvent>(OnEntitySpoke);
        }

        public SoundSpecifier? GetSpeechSound(Entity<SpeechComponent> ent, string message)
        {
            if (ent.Comp.SpeechSounds == null)
                return null;

            // Play speech sound
            SoundSpecifier? contextSound;
            var prototype = ProtoMan.Index<SpeechSoundsPrototype>(ent.Comp.SpeechSounds);

            // Different sounds for ask/exclaim based on last character
            contextSound = message[^1] switch
            {
                '?' => prototype.AskSound,
                '!' => prototype.ExclaimSound,
                _ => prototype.SaySound
            };

            // Use exclaim sound if most characters are uppercase.
            int uppercaseCount = 0;
            for (int i = 0; i < message.Length; i++)
            {
                if (char.IsUpper(message[i]))
                    uppercaseCount++;
            }
            if (uppercaseCount > (message.Length / 2))
            {
                contextSound = prototype.ExclaimSound;
            }

            var scale = (float) _random.NextGaussian(1, prototype.Variation);
            contextSound.Params = ent.Comp.AudioParams.WithPitchScale(scale);
            return contextSound;
        }

        private void OnEntitySpoke(EntityUid uid, SpeechComponent component, EntitySpokeEvent args)
        {
            if (component.SpeechSounds == null)
                return;

            var currentTime = _gameTiming.CurTime;
            var cooldown = TimeSpan.FromSeconds(component.SoundCooldownTime);

            // Ensure more than the cooldown time has passed since last speaking
            if (currentTime - component.LastTimeSoundPlayed < cooldown)
                return;

            var sound = GetSpeechSound((uid, component), args.Message);
            component.LastTimeSoundPlayed = currentTime;
            // Start Box Change: Speech sound muting
            //_audio.PlayPvs(sound, uid);
            var filter = Filter.Pvs(uid).RemoveWhere(s => !_speciesMuting.ShouldPlayVoicelines(uid, s));
            if (filter.Count == 0)
                return;
            _audio.PlayEntity(sound, filter, uid, true);
            // End Box Change
        }
    }
}
