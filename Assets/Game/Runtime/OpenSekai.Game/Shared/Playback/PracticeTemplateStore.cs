using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using OpenSekai.Shared.Storage;
using UnityEngine;

namespace OpenSekai.Shared.Playback
{
    public sealed class PracticeTemplateStore
    {
        private static readonly object _fileLock = new object();
        private readonly string _path;

        private static PracticeTemplateStore _shared;
        public static PracticeTemplateStore Shared => _shared ??= new PracticeTemplateStore(
            Path.Combine(Application.persistentDataPath, "practice-templates.json"));

        public PracticeTemplateStore(string path)
        {
            _path = path ?? throw new ArgumentNullException(nameof(path));
        }

        public IReadOnlyList<PracticeTemplate> Load()
        {
            lock (_fileLock) return ReadTemplates().AsReadOnly();
        }

        /// <summary>Inserts or replaces by name. A read or write failure preserves the previous library.</summary>
        public void Save(PracticeTemplate template)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            lock (_fileLock)
            {
                var templates = ReadTemplates();
                int index = templates.FindIndex(saved => saved.Name == template.Name);
                if (index < 0) templates.Add(template);
                else templates[index] = template;

                AtomicFile.WriteAllText(_path, JsonConvert.SerializeObject(templates, Formatting.Indented));
            }
        }

        private List<PracticeTemplate> ReadTemplates()
        {
            if (!File.Exists(_path)) return new List<PracticeTemplate>();
            var templates = JsonConvert.DeserializeObject<List<PracticeTemplate>>(File.ReadAllText(_path));
            if (templates == null) throw new InvalidDataException("The practice template library is empty or invalid.");

            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var template in templates)
            {
                if (template == null || !names.Add(template.Name))
                    throw new InvalidDataException("The practice template library contains an empty entry or duplicate name.");
            }
            return templates;
        }
    }
}
