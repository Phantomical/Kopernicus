/**
* Kopernicus Planetary System Modifier
* -------------------------------------------------------------
* This library is free software; you can redistribute it and/or
* modify it under the terms of the GNU Lesser General Public
* License as published by the Free Software Foundation; either
* version 3 of the License, or (at your option) any later version.
*
* This library is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
* Lesser General Public License for more details.
*
* You should have received a copy of the GNU Lesser General Public
* License along with this library; if not, write to the Free Software
* Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston,
* MA 02110-1301  USA
*
* This library is intended to be used as a plugin for Kerbal Space Program
* which is copyright of TakeTwo Interactive. Your usage of Kerbal Space Program
* itself is governed by the terms of its EULA, not the license above.
*
* https://kerbalspaceprogram.com
*/

using System;
using UnityEngine;

namespace Kopernicus.Components;

public class PQSMod_ScatterMask : PQSMod
{
    /// <summary>
    /// Name of the scatter mask that Kopernicus adds around the KSC (see
    /// <c>Injector.AddKscScatterMask</c>). <c>Components.KSC</c> re-centers
    /// the mask with this name when a <c>SpaceCenter</c> node moves the KSC.
    /// </summary>
    public const string KscMaskName = "KopernicusKSCScatterMask";

    #region Config
    // These define the center of the scatter mask on the surface, in degrees.
    public double lat;
    public double lon;

    // The width, in meters, of one side of the mask.
    public double width;

    // Rotation of the texture, in degrees, about the mask center. At 0 the
    // texture's +V axis points north and its +U axis points east.
    public float angle;

    // Turn this on to show the color map for debugging reasons.
    public bool debugShowColorMap;

    // The mask texture.
    public MapSO mask;
    #endregion

    #region Derived State
    // Unit direction from the planet center to the mask center.
    private Vector3d normal;

    // Surface tangent directions, in planet-local coordinates, that the
    // texture's U and V axes are mapped along. These are unit vectors.
    private Vector3d uAxis;
    private Vector3d vAxis;
    #endregion

    public override void OnSetup()
    {
        if (debugShowColorMap)
            requirements |= PQS.ModiferRequirements.MeshColorChannel;

        SetCenter(lat, lon);

        if (mask == null)
        {
            Debug.LogWarning("PQSMod_ScatterMask: No mask specified, planet: " + sphere.name);
            modEnabled = false;
        }

        if (!(width > 0.0))
        {
            Debug.LogWarning("PQSMod_ScatterMask: width must be positive, planet: " + sphere.name);
            modEnabled = false;
        }
    }

    /// <summary>
    /// Move the mask center to the given latitude and longitude, in degrees.
    /// </summary>
    public void SetCenter(double latitude, double longitude)
    {
        lat = latitude;
        lon = longitude;
        UpdateBasis(Planetarium.SphericalVector(lat * MathUtility.Deg2Rad, lon * MathUtility.Deg2Rad).xzy);
    }

    /// <summary>
    /// Move the mask center to the surface point in the given body-local
    /// direction (e.g. <see cref="PQSCity.repositionRadial"/>).
    /// </summary>
    public void SetCenter(Vector3d radial)
    {
        Vector3d dir = radial.normalized;

        // Body-local +Y is the north pole, mirroring CelestialBody.GetRelSurfaceNVector.
        lat = Math.Asin(Math.Max(-1.0, Math.Min(1.0, dir.y))) * MathUtility.Rad2Deg;
        lon = Math.Atan2(dir.z, dir.x) * MathUtility.Rad2Deg;
        UpdateBasis(dir);
    }

    private void UpdateBasis(Vector3d dir)
    {
        normal = dir;

        // Build an east/north tangent basis at the mask center. The planet's
        // local +Y axis is its north pole.
        var east = Vector3d.Cross(normal, Vector3d.up);
        if (east.sqrMagnitude < 1e-12)
            east = Vector3d.right; // At a pole every direction is south; pick one.
        east.Normalize();
        var north = Vector3d.Cross(east, normal);

        // Fold the texture rotation into the basis so that each vertex only
        // needs two dot products.
        var cos = Math.Cos(angle * MathUtility.Deg2Rad);
        var sin = Math.Sin(angle * MathUtility.Deg2Rad);
        uAxis = east * cos + north * sin;
        vAxis = north * cos - east * sin;
    }

    public override void OnVertexBuild(PQS.VertexBuildData data)
    {
        if (debugShowColorMap)
            data.vertColor = Color.black;

        if (sphere.isBuildingMaps)
            return;

        var ratio = sphere.radius / (0.5 * width);
        var dir = data.directionFromCenter;

        // Check that we are on the same side of the planet as the mask
        if (Vector3d.Dot(dir, normal) <= 0.0)
            return;
        var u = (Vector3d.Dot(dir, uAxis) * ratio + 1.0) * 0.5;
        var v = (Vector3d.Dot(dir, vAxis) * ratio + 1.0) * 0.5;

        if (u < 0.0 || u > 1.0)
            return;
        if (v < 0.0 || v > 1.0)
            return;

        var value = mask.GetPixelFloat(u, v);
        var allowScatter = value > 0.01f;

        data.allowScatter &= allowScatter;
        if (debugShowColorMap)
            data.vertColor = allowScatter ? Color.green : Color.red;
    }
}
