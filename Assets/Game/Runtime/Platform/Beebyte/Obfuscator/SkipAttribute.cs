using System;

namespace Beebyte.Obfuscator
{
    // TODO: replace with the full Beebyte Obfuscator plugin when available.
    // SkipAttribute is a marker attribute; the full plugin is not present in this build.
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
    public sealed class SkipAttribute : Attribute
    {
        public SkipAttribute()
        {
        }
    }
}
