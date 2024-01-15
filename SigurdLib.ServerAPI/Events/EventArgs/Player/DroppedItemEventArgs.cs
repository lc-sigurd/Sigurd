using Unity.Netcode;
using UnityEngine;

namespace Sigurd.ServerAPI.Events.EventArgs.Player
{
    /// <summary>
    /// Contains all the information after a <see cref="Common.Features.SPlayer"/> drops an <see cref="Common.Features.SItem"/>.
    /// </summary>
    public class DroppedItemEventArgs : System.EventArgs
    {
        /// <summary>
        /// Gets the <see cref="Common.Features.SPlayer"/> that dropped the <see cref="Item"/>.
        /// </summary>
        public Common.Features.SPlayer Player { get; }

        /// <summary>
        /// Gets the <see cref="Common.Features.SItem"/> that was dropped.
        /// </summary>
        public Common.Features.SItem Item { get; }

    /// <summary>
    /// Gets whether or not the <see cref="Item"/> was placed.
    /// </summary>
    public bool Placing { get; }

    /// <summary>
    /// Gets the target end position of the <see cref="Item"/>.
    /// </summary>
    public Vector3 TargetPosition { get; }

    /// <summary>
    /// Gets the target Y rotation of the <see cref="Item"/>.
    /// </summary>
    public int FloorYRotation { get; }

    /// <summary>
    /// Gets the parent <see cref="NetworkObject"/>, if there is one.
    /// </summary>
    public NetworkObject? ParentObjectTo { get; }

    /// <summary>
    /// Gets whether or not to match the rotation of the <see cref="ParentObjectTo"/>.
    /// </summary>
    public bool MatchRotationOfParent { get; }

    /// <summary>
    /// Gets whether or not the <see cref="Item"/> was dropped in the ship.
    /// </summary>
    public bool DroppedInShip { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DroppedItemEventArgs"/> class.
        /// </summary>
        /// <param name="player"><inheritdoc cref="Player"/></param>
        /// <param name="item"><inheritdoc cref="Item"/></param>
        /// <param name="placeObject"><inheritdoc cref="Placing"/></param>
        /// <param name="targetPosition"><inheritdoc cref="TargetPosition"/></param>
        /// <param name="floorYRotation"><inheritdoc cref="FloorYRotation"/></param>
        /// <param name="parentObjectTo"><inheritdoc cref="ParentObjectTo"/></param>
        /// <param name="matchRotationOfParent"><inheritdoc cref="MatchRotationOfParent"/></param>
        /// <param name="droppedInShip"><inheritdoc cref="DroppedInShip"/></param>
        public DroppedItemEventArgs(Common.Features.SPlayer player, Common.Features.SItem item, bool placeObject, Vector3 targetPosition,
            int floorYRotation, NetworkObject? parentObjectTo, bool matchRotationOfParent, bool droppedInShip)
        {
            Player = player;
            Item = item;
            Placing = placeObject;
            TargetPosition = targetPosition;
            FloorYRotation = floorYRotation;
            ParentObjectTo = parentObjectTo;
            MatchRotationOfParent = matchRotationOfParent;
            DroppedInShip = droppedInShip;
        }
    }
}
