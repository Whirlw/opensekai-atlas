using UnityEngine;

namespace Sekai
{
	public class EffectPool : MonoBehaviour
	{
		private ParticleSystemController[] _pool;

		private int _spawnIndex;

		public void Setup(GameObject prefab, int poolCount)
		{
			_pool = new ParticleSystemController[Mathf.Max(poolCount, 0)];
			for (int i = 0; i < _pool.Length; i++)
			{
				if (prefab == null)
				{
					continue;
				}

				GameObject instance = Instantiate(prefab, transform);
				_pool[i] = instance.AddComponent<ParticleSystemController>();
			}

			if (_pool.Length > 0 && _pool[0] != null)
			{
				_pool[0].Play();
				_pool[0].Stop();
			}
		}

		public ParticleSystemController Spawn()
		{
			if (_pool == null || _pool.Length == 0)
			{
				return null;
			}

			ParticleSystemController controller = _pool[_spawnIndex];
			_spawnIndex = (_spawnIndex + 1) % _pool.Length;
			return controller;
		}

		public EffectPool()
		{
		}
	}
}
