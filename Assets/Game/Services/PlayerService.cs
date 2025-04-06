using Assets.Game.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Game.Services
{
    public class PlayerService : IPlayerService
    {
        //camera can move getter and setter
        private bool _cameraCanMove = true;
        public bool CameraCanMove
        {
            get { return _cameraCanMove; }
            set { _cameraCanMove = value; }
        }
    }
}
