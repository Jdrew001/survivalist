using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Zenject;

namespace Assets.Game.Managers
{
    public abstract class BaseManager : MonoBehaviour
    {
        protected virtual void Awake()
        {
            // Automatically inject this component using the ProjectContext's container.
            if (ProjectContext.Instance != null)
            {
                ProjectContext.Instance.Container.Inject(this);
            }
            else
            {
                Debug.LogWarning("ProjectContext.Instance is null. Injection failed for " + gameObject.name);
            }
        }
    }
}
