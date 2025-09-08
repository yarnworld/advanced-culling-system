using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Tutorial
{
    public class Spawner : MonoBehaviour
    {
        [SerializeField]
        private GameObject _prefab;

        [SerializeField]
        private int _maxInstances = 2000;

        [SerializeField]
        private float _delay = 0.02f;

        private int _counter;
        private float _timer;


        private void Update()
        {
            _timer += Time.deltaTime;

            if (_timer > _delay)
            {
                Spawn();

                _timer = 0;
                _counter++;

                if (_counter > _maxInstances)
                    enabled = false;
            }
        }

        private void Spawn()
        {
            GameObject instance = Instantiate(_prefab);

            instance.transform.position = transform.position;
            instance.transform.rotation = Random.rotation;
            instance.transform.localScale = Vector3.one * Random.Range(1, 2.5f);
        }
    }
}
