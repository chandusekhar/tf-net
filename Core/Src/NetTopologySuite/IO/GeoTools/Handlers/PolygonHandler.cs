using System;
using System.Collections;
using System.Diagnostics;



using Topology.Algorithm;
using Topology.Geometries;

namespace Topology.IO
{
	/// <summary>
	/// Converts a Shapefile point to a OGIS Polygon.
	/// </summary>
	public class PolygonHandler : ShapeHandler
	{		
		/// <summary>
		/// Initializes a new instance of the PolygonHandler class.
		/// </summary>
		public PolygonHandler() { }	

		/// <summary>
		/// The ShapeType this handler handles.
		/// </summary>
        public override ShapeGeometryTypes ShapeType
		{
			get
			{
                return ShapeGeometryTypes.Polygon;
			}
		}

		/// <summary>
		/// Reads a stream and converts the shapefile record to an equilivent geometry object.
		/// </summary>
		/// <param name="file">The stream to read.</param>
		/// <param name="geometryFactory">The geometry factory to use when making the object.</param>
		/// <returns>The Geometry object that represents the shape file record.</returns>
        public override IGeometry Read(BigEndianBinaryReader file, IGeometryFactory geometryFactory)
		{
			int shapeTypeNum = file.ReadInt32();
            ShapeGeometryTypes shapeType = (ShapeGeometryTypes)Enum.Parse(typeof(ShapeGeometryTypes), shapeTypeNum.ToString());
            if (shapeType == ShapeGeometryTypes.NullShape)
                return null;

            if ( ! ( shapeType == ShapeGeometryTypes.Polygon  || shapeType == ShapeGeometryTypes.PolygonM ||
                     shapeType == ShapeGeometryTypes.PolygonZ || shapeType == ShapeGeometryTypes.PolygonZM))	
				throw new ShapefileException("Attempting to load a non-polygon as polygon.");

			// Read and for now ignore bounds.
			double[] box = new double[4];
			for (int i = 0; i < 4; i++) 
				box[i] = file.ReadDouble();

			int[] partOffsets;        
			int numParts = file.ReadInt32();
			int numPoints = file.ReadInt32();
			partOffsets = new int[numParts];
			for (int i = 0; i < numParts; i++)
				partOffsets[i] = file.ReadInt32();

			ArrayList shells = new ArrayList();
			ArrayList holes = new ArrayList();

			int start, finish, length;
			for (int part = 0; part < numParts; part++)
			{
				start = partOffsets[part];
				if (part == numParts - 1)
					 finish = numPoints;
				else finish = partOffsets[part + 1];
				length = finish - start;
                CoordinateList points = new CoordinateList();
				points.Capacity = length;
				for (int i = 0; i < length; i++)
				{
					ICoordinate external = new Coordinate(file.ReadDouble(), file.ReadDouble() );					
                    geometryFactory.PrecisionModel.MakePrecise( external);
                    ICoordinate internalCoord = external;

                    // Thanks to Abhay Menon!
                    if (!Double.IsNaN(internalCoord.Y) && !Double.IsNaN(internalCoord.X))
                       points.Add(internalCoord);
 				}

                if (points.Count > 0) // Thanks to Abhay Menon!
                {
                    ILinearRing ring = geometryFactory.CreateLinearRing(points.ToArray());

                    // If shape have only a part, jump orientation check and add to shells
                    if (numParts == 1)
                        shells.Add(ring);
                    else
                    {
                        // Orientation check
                        if (CGAlgorithms.IsCCW(points.ToArray()))
                             holes.Add(ring);
                        else shells.Add(ring);
                    }
                }
			}

			// Now we have a list of all shells and all holes
			ArrayList holesForShells = new ArrayList(shells.Count);
			for (int i = 0; i < shells.Count; i++)
				holesForShells.Add(new ArrayList());
			// Find holes
			for (int i = 0; i < holes.Count; i++)
			{
				ILinearRing testRing = (ILinearRing) holes[i];
				ILinearRing minShell = null;
				IEnvelope minEnv = null;
                IEnvelope testEnv = testRing.EnvelopeInternal;
                ICoordinate testPt = testRing.GetCoordinateN(0);
				ILinearRing tryRing;
				for (int j = 0; j < shells.Count; j++)
				{
					tryRing = (ILinearRing) shells[j];
                    IEnvelope tryEnv = tryRing.EnvelopeInternal;
					if (minShell != null)
                        minEnv = minShell.EnvelopeInternal;
					bool isContained = false;
					CoordinateList coordList = new CoordinateList(tryRing.Coordinates);
					if (tryEnv.Contains(testEnv)
                        && (CGAlgorithms.IsPointInRing(testPt, coordList.ToArray()) 
                        || (PointInList(testPt, coordList)))) 				
						isContained = true;

                    // Check if this new containing ring is smaller than the current minimum ring
                    if (isContained)
                    {
                        if (minShell == null || minEnv.Contains(tryEnv))
                            minShell = tryRing;             

                        // Suggested by Brian Macomber and added 3/28/2006:
                        // holes were being found but never added to the holesForShells array
                        // so when converted to geometry by the factory, the inner rings were never created.
                        ArrayList holesForThisShell = (ArrayList) holesForShells[j];
                        holesForThisShell.Add(testRing);
                    }
				}
            }

            //If we have Z-coordinates, read them..
            if (shapeType == ShapeGeometryTypes.PolygonZ || shapeType == ShapeGeometryTypes.PolygonZM)
            {
                //z-Bounds
                double zMin = file.ReadDouble();
                double zMax = file.ReadDouble();
                for (int part = 0; part < numParts; part++)
                {
                    start = partOffsets[part];
                    if (part == numParts - 1)
                        finish = numPoints;
                    else finish = partOffsets[part + 1];
                    length = finish - start;
                    for (int i = 0; i < length; i++)
                    {
                        double val = file.ReadDouble();
                        //lines[part].Coordinates[i].Z = val;
                    }

                }
            }

            //If we have M-coordinates, read them..
            if (shapeType == ShapeGeometryTypes.PolygonM || shapeType == ShapeGeometryTypes.PolygonZM)
            {
                //m-Bounds
                double mMin = file.ReadDouble();
                double mMax = file.ReadDouble();
                for (int part = 0; part < numParts; part++)
                {
                    start = partOffsets[part];
                    if (part == numParts - 1)
                        finish = numPoints;
                    else finish = partOffsets[part + 1];
                    length = finish - start;
                    for (int i = 0; i < length; i++)
                    {
                        double val = file.ReadDouble();
                        //dont store..
                    }

                }
            }




			IPolygon[] polygons = new IPolygon[shells.Count];
			for (int i = 0; i < shells.Count; i++)
                polygons[i] = (geometryFactory.CreatePolygon((ILinearRing) shells[i], 
                    (ILinearRing[]) ((ArrayList)holesForShells[i]).ToArray(typeof(ILinearRing))));
        
			if (polygons.Length == 1)
				return polygons[0];
			// It's a multi part
			return geometryFactory.CreateMultiPolygon(polygons);
		}

		/// <summary>
		/// Writes a Geometry to the given binary wirter.
		/// </summary>
		/// <param name="geometry">The geometry to write.</param>
		/// <param name="file">The file stream to write to.</param>
		/// <param name="geometryFactory">The geometry factory to use.</param>
		public override void Write(IGeometry geometry, System.IO.BinaryWriter file, IGeometryFactory geometryFactory)
		{
            // This check seems to be not useful and slow the operations...
			// if (!geometry.IsValid)    
			// Trace.WriteLine("Invalid polygon being written.");

			IGeometryCollection multi = null;
			if (geometry is IGeometryCollection)
				multi = (IGeometryCollection) geometry;
			else 
			{
				GeometryFactory gf = new GeometryFactory(geometry.PrecisionModel);				
				multi = gf.CreateMultiPolygon(new IPolygon[] { (IPolygon) geometry, } );
			}

			file.Write(int.Parse(Enum.Format(typeof(ShapeGeometryTypes), this.ShapeType, "d")));

            IEnvelope box = multi.EnvelopeInternal;
			IEnvelope bounds = ShapeHandler.GetEnvelopeExternal(geometryFactory.PrecisionModel,  box);
			file.Write(bounds.MinX);
			file.Write(bounds.MinY);
			file.Write(bounds.MaxX);
			file.Write(bounds.MaxY);
        
			int numParts = GetNumParts(multi);
			int numPoints = multi.NumPoints;
			file.Write(numParts);
			file.Write(numPoints);
        			
			// write the offsets to the points
			int offset=0;
			for (int part = 0; part < multi.NumGeometries; part++)
			{
				// offset to the shell points
				IPolygon polygon = (IPolygon) multi.Geometries[part];
				file.Write(offset);
				offset = offset + polygon.ExteriorRing.NumPoints;

				// offstes to the holes
				foreach (ILinearRing ring in polygon.InteriorRings)
				{
					file.Write(offset);
					offset = offset + ring.NumPoints;
				}	
			}

			// write the points 
			for (int part = 0; part < multi.NumGeometries; part++)
			{
                IPolygon poly = (IPolygon) multi.Geometries[part];
				ICoordinate[] points = poly.ExteriorRing.Coordinates;
				WriteCoords(new CoordinateList(points), file, geometryFactory);
				foreach(ILinearRing ring in poly.InteriorRings)
				{
                    ICoordinate[] points2 = ring.Coordinates;					
					WriteCoords(new CoordinateList(points2), file, geometryFactory);
				}
			}
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="points"></param>
        /// <param name="file"></param>
        /// <param name="geometryFactory"></param>
		private void WriteCoords(CoordinateList points, System.IO.BinaryWriter file, IGeometryFactory geometryFactory)
		{
			ICoordinate external;
			foreach (ICoordinate point in points)
			{
				// external = geometryFactory.PrecisionModel.ToExternal(point);
                external = point;
				file.Write(external.X);
				file.Write(external.Y);
			}
		}

		/// <summary>
		/// Gets the length of the shapefile record using the geometry passed in.
		/// </summary>
		/// <param name="geometry">The geometry to get the length for.</param>
		/// <returns>The length in bytes this geometry is going to use when written out as a shapefile record.</returns>
		public override int GetLength(IGeometry geometry)
		{
			int numParts = GetNumParts(geometry);
			return (22 + (2 * numParts) + geometry.NumPoints * 8);
		}
		
        /// <summary>
        /// 
        /// </summary>
        /// <param name="geometry"></param>
        /// <returns></returns>
		private int GetNumParts(IGeometry geometry)
		{
			int numParts = 0;
			if (geometry is IMultiPolygon)
            {
                IMultiPolygon mpoly = geometry as IMultiPolygon;
                foreach (IPolygon poly in mpoly.Geometries)
					numParts = numParts + poly.InteriorRings.Length + 1;
            }
			else if (geometry is IPolygon)
				numParts = ((IPolygon) geometry).InteriorRings.Length + 1;
			else throw new InvalidOperationException("Should not get here.");
			return numParts;
		}

		/// <summary>
		/// Test if a point is in a list of coordinates.
		/// </summary>
		/// <param name="testPoint">TestPoint the point to test for.</param>
		/// <param name="pointList">PointList the list of points to look through.</param>
		/// <returns>true if testPoint is a point in the pointList list.</returns>
		private bool PointInList(ICoordinate testPoint, CoordinateList pointList) 
		{
			foreach(ICoordinate p in pointList)
				if (p.Equals2D(testPoint))
					return true;
			return false;
		}
	}
}
