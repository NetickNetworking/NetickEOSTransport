using Epic.OnlineServices.P2P;
using Netick;
using Netick.Unity;
using NetickEOSTransport;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace NetickEOSTransport
{
  [CreateAssetMenu(fileName = "EOSTransportProvider", menuName = "Netick/Transport/EOSTransportProvider", order = 1)]
  public class EOSTransportProvider : NetworkTransportProvider
  {
    public RelayControl RelayControl = RelayControl.AllowRelays;

    public override NetworkTransport MakeTransportInstance() => new EOSTransport(this);
  }
}
