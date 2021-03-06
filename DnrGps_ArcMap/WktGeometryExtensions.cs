﻿using System;
using System.Linq;
using System.Text;
using ESRI.ArcGIS.Framework;
using ESRI.ArcGIS.Geometry;

namespace DnrGps_ArcMap
{
    /// <summary>
    /// Transform ESRI geometries to the Well-known Text representation, as defined in
    /// section 7 of http://www.opengeospatial.org/standards/sfa (version 1.2.1 dated 2011-05-28)
    /// </summary>
    internal static class WktGeometryExtensions
    {

        internal static string ToWellKnownText(this IGeometry geometry)
        {
            return BuildWellKnownText(geometry);
        }


        internal static IGeometry ToGeometry(this string wkt, IObjectFactory objectFactory)
        {
            return BuildGeometry(wkt, objectFactory);
        }


        #region Private methods for WKT construction

        private static string BuildWellKnownText(IGeometry geometry)
        {
            if (geometry is IPoint)
                return BuildWellKnownText(geometry as IPoint);
            if (geometry is IMultipoint)
                return BuildWellKnownText(geometry as IMultipoint);
            if (geometry is IPolyline)
                return BuildWellKnownText(geometry as IPolyline);
            if (geometry is IPolygon)
                return BuildWellKnownText(geometry as IPolygon);
            if (geometry is IGeometryBag)
                throw new NotImplementedException("Geometry bags to well known text is not yet supported");
            return string.Empty;
        }


        private static string BuildWellKnownText(IPoint point)
        {
            return string.Format("POINT ({0} {1})", point.X, point.Y);
        }


        private static string BuildWellKnownText(IMultipoint points)
        {
            //Example - MULTIPOINT ((10 40), (40 30), (20 20), (30 10))
            //Example - MULTIPOINT (10 40, 40 30, 20 20, 30 10)
            return "MULTIPOINT " + BuildWellKnownText(points as IPointCollection);
        }


        private static string BuildWellKnownText(IPolyline polyline)
        {
            //Example - LINESTRING (30 10, 10 30, 40 40)
            //Example - MULTILINESTRING ((10 10, 20 20, 10 40),(40 40, 30 30, 40 20, 30 10))
            var geometryCollection = polyline as IGeometryCollection;
            if (geometryCollection == null)
                return string.Empty;
            int partCount = geometryCollection.GeometryCount;
            if (partCount == 0)
                return string.Empty;
            if (partCount == 1)
                return "LINESTRING " + BuildWellKnownText(polyline as IPointCollection);
            return "MULTILINESTRING " + BuildWellKnownText(geometryCollection);
        }


        private static string BuildWellKnownText(IPolygon polygon)
        {
            //Example - POLYGON ((30 10, 10 20, 20 40, 40 40, 30 10))
            //Example - POLYGON ((35 10, 10 20, 15 40, 45 45, 35 10),(20 30, 35 35, 30 20, 20 30))
            //Example - MULTIPOLYGON (((30 20, 10 40, 45 40, 30 20)),((15 5, 40 10, 10 20, 5 10, 15 5)))
            //Example - MULTIPOLYGON (((40 40, 20 45, 45 30, 40 40)),((20 35, 45 20, 30 5, 10 10, 10 30, 20 35),(30 20, 20 25, 20 15, 30 20)))
            var geometryCollection = polygon as IGeometryCollection;
            if (geometryCollection == null)
                return string.Empty;
            int partCount = geometryCollection.GeometryCount;
            if (partCount == 0)
                return string.Empty;

            //FIXME - ArcObjects does not have a "multipolygon", however a polygon with multiple exterior rings needs to be a multipolygon in WKT
            //ArcGIS does not differentiate multi-ring polygons and multipolygons
            //Each polygon is simply a collection of rings in any order.
            //in ArcObjects a ring is clockwise for outer, and counterclockwise for inner (interior is on your right)
            //FIXME - In Wkt, exterior rings are counterclockwise, and interior are clockwise (interior is on your left)
            //FIXME - In Wkt, a polygon is one exterior, and zero or more interior rings

            //if (polygon.ExteriorRingCount > 1)
            //    return "MULTIPOLYGON " + BuildWellKnownText(geometryCollection);
            return "POLYGON " + BuildWellKnownText(geometryCollection);

        }


        private static string BuildWellKnownText(IGeometryCollection geometries)
        {
            //Example ((10 10, 20 20, 10 40))
            //Example ((10 10, 20 20, 10 40),(40 40, 30 30, 40 20, 30 10))
            var sb = new StringBuilder();
            int partCount = geometries.GeometryCount;
            if (partCount < 1)
                return string.Empty;
            sb.AppendFormat("({0}", BuildWellKnownText(geometries.Geometry[0] as IPointCollection));
            for (int i = 1; i < partCount; i++)
                sb.AppendFormat(",{0}", BuildWellKnownText(geometries.Geometry[i] as IPointCollection));
            sb.Append(")");
            return sb.ToString();
        }


        private static string BuildWellKnownText(IPointCollection points)
        {
            //Example - (10 40)
            //Example - (10 40, 40 30, 20 20, 30 10)
            var sb = new StringBuilder();
            int pointCount = points.PointCount;
            if (pointCount < 1)
                return string.Empty;
            sb.AppendFormat("({0} {1}", points.Point[0].X, points.Point[0].Y);
            for (int i = 1; i < pointCount; i++)
                sb.AppendFormat(",{0} {1}", points.Point[i].X, points.Point[i].Y);
            sb.Append(")");
            return sb.ToString();
        }

        #endregion


        #region Private methods for IGeometry construction

        private static IGeometry BuildGeometry(string s, IObjectFactory objectFactory)
        {
            var wkt = new WktText(s);

            switch (wkt.Type)
            {
                case WktType.None:
                    return null;
                case WktType.Point:
                    return BuildPoint(wkt, objectFactory);
                case WktType.LineString:
                    return BuildPolyline(wkt, objectFactory);
                case WktType.Polygon:
                    return BuildPolygon(wkt, objectFactory);
                case WktType.Triangle:
                    return BuildPolygon(wkt, objectFactory);
                case WktType.PolyhedralSurface:
                    return BuildMultiPatch(wkt, objectFactory);
                case WktType.Tin:
                    return BuildMultiPolygon(wkt, objectFactory);
                case WktType.MultiPoint:
                    return BuildMultiPoint(wkt, objectFactory);
                case WktType.MultiLineString:
                    return BuildMultiPolyline(wkt, objectFactory);
                case WktType.MultiPolygon:
                    return BuildMultiPolygon(wkt, objectFactory);
                case WktType.GeometryCollection:
                    return BuildGeometryCollection(wkt, objectFactory);
                default:
                    throw new ArgumentOutOfRangeException("s", "Unsupported geometry type: " + wkt.Type);
            }
        }

        private static IGeometry BuildPoint(WktText wkt, IObjectFactory objectFactory)
        {
            return BuildPoint(wkt.Token, wkt, objectFactory);
        }

        private static IGeometry BuildMultiPoint(WktText wkt, IObjectFactory objectFactory)
        {
            var multiPoint = (IPointCollection)objectFactory.Create("esriGeometry.Multipoint");
            //IPointCollection multiPoint = new MultipointClass();

            foreach (var point in wkt.Token.Tokens)
            {
                multiPoint.AddPoint(BuildPoint(point, wkt, objectFactory));
            }
            ((ITopologicalOperator)multiPoint).Simplify();
            var geometry = multiPoint as IGeometry;
            MakeZmAware(geometry, wkt.HasZ, wkt.HasM);
            return geometry;
        }

        private static IGeometry BuildPolyline(WktText wkt, IObjectFactory objectFactory)
        {
            var multiPath = (IGeometryCollection)objectFactory.Create("esriGeometry.Polyline");
            //IGeometryCollection multiPath = new PolylineClass();

            var path = BuildPath(wkt.Token, wkt, objectFactory);
            ((ITopologicalOperator)multiPath).Simplify();
            multiPath.AddGeometry(path);
            var geometry = multiPath as IGeometry;
            MakeZmAware(geometry, wkt.HasZ, wkt.HasM);
            return geometry;
        }

        private static IGeometry BuildMultiPolyline(WktText wkt, IObjectFactory objectFactory)
        {
            var multiPath = (IGeometryCollection)objectFactory.Create("esriGeometry.Polyline");
            //IGeometryCollection multiPath = new PolylineClass();

            foreach (var lineString in wkt.Token.Tokens)
            {
                var path = BuildPath(lineString, wkt, objectFactory);
                multiPath.AddGeometry(path);
            }
            ((ITopologicalOperator)multiPath).Simplify();
            var geometry = multiPath as IGeometry;
            MakeZmAware(geometry, wkt.HasZ, wkt.HasM);
            return geometry;
        }

        private static IGeometry BuildPolygon(WktText wkt, IObjectFactory objectFactory)
        {
            var multiRing = (IGeometryCollection)objectFactory.Create("esriGeometry.Polygon");
            //IGeometryCollection multiRing = new PolygonClass();

            foreach (var ringString in wkt.Token.Tokens)
            {
                var ring = BuildRing(ringString, wkt, objectFactory);
                multiRing.AddGeometry(ring);
            }
            ((ITopologicalOperator)multiRing).Simplify();
            var geometry = multiRing as IGeometry;
            MakeZmAware(geometry, wkt.HasZ, wkt.HasM);
            return geometry;
        }

        private static IGeometry BuildMultiPolygon(WktText wkt, IObjectFactory objectFactory)
        {
            var multiRing = (IGeometryCollection)objectFactory.Create("esriGeometry.Polygon");
            //IGeometryCollection multiRing = new PolygonClass();

            foreach (var polygonString in wkt.Token.Tokens)
            {
                foreach (var ringString in polygonString.Tokens)
                {
                    var ring = BuildRing(ringString, wkt, objectFactory);
                    multiRing.AddGeometry(ring);
                }
            }
            ((ITopologicalOperator)multiRing).Simplify();
            var geometry = multiRing as IGeometry;
            MakeZmAware(geometry, wkt.HasZ, wkt.HasM);
            return geometry;
        }

        private static IGeometry BuildMultiPatch(WktText wkt, IObjectFactory objectFactory)
        {
            var multiPatch = (IGeometryCollection)objectFactory.Create("esriGeometry.MultiPatch");
            //IGeometryCollection multiPatch = new MultiPatchClass();

            foreach (var polygonString in wkt.Token.Tokens)
            {
                bool isOuter = true;
                foreach (var ringString in polygonString.Tokens)
                {
                    var ring = BuildRing(ringString, wkt, objectFactory);
                    multiPatch.AddGeometry(ring);
                    ((IMultiPatch)multiPatch).PutRingType(ring, isOuter
                                                                  ? esriMultiPatchRingType.esriMultiPatchOuterRing
                                                                  : esriMultiPatchRingType.esriMultiPatchInnerRing);
                    isOuter = false;
                }
            }
            ((ITopologicalOperator)multiPatch).Simplify();
            var geometry = multiPatch as IGeometry;
            MakeZmAware(geometry, wkt.HasZ, wkt.HasM);
            return geometry;
        }

        private static IGeometry BuildGeometryCollection(WktText wkt, IObjectFactory objectFactory)
        {
            var geometryBag = (IGeometryCollection)objectFactory.Create("esriGeometry.GeometryBag");
            //IGeometryCollection geometryBag = new GeometryBagClass();

            foreach (var geomToken in wkt.Token.Tokens)
            {
                var geom = BuildGeometry(geomToken.ToString(), objectFactory);
                geometryBag.AddGeometry(geom);
            }
            var geometry = geometryBag as IGeometry;
            MakeZmAware(geometry, wkt.HasZ, wkt.HasM);
            return geometry;
        }



        private static IPath BuildPath(WktToken token, WktText wkt, IObjectFactory objectFactory)
        {
            var path = (IPointCollection)objectFactory.Create("esriGeometry.Path");
            //IPointCollection multiPoint = new PathClass();
            foreach (var point in token.Tokens)
            {
                path.AddPoint(BuildPoint(point, wkt, objectFactory));
            }
            var geometry = path as IGeometry;
            MakeZmAware(geometry, wkt.HasZ, wkt.HasM);
            return (IPath)path;
        }

        private static IRing BuildRing(WktToken token, WktText wkt, IObjectFactory objectFactory)
        {
            var ring = (IPointCollection)objectFactory.Create("esriGeometry.Ring");
            //IPointCollection multiPoint = new RingClass();
            foreach (var point in token.Tokens)
            {
                ring.AddPoint(BuildPoint(point, wkt, objectFactory));
            }
            MakeZmAware((IGeometry)ring, wkt.HasZ, wkt.HasM);
            return (IRing)ring;
        }

        private static IPoint BuildPoint(WktToken token, WktText wkt, IObjectFactory objectFactory)
        {
            var coordinates = token.Coords.ToArray();

            int partCount = coordinates.Length;
            if (!wkt.HasZ && !wkt.HasM && partCount != 2)
                throw new ArgumentException("Mal-formed WKT, wrong number of elements, expecting x and y");
            if (wkt.HasZ && !wkt.HasM && partCount != 3)
                throw new ArgumentException("Mal-formed WKT, wrong number of elements, expecting x y z");
            if (!wkt.HasZ && wkt.HasM && partCount != 3)
                throw new ArgumentException("Mal-formed WKT, wrong number of elements, expecting x y m");
            if (wkt.HasZ && wkt.HasM && partCount != 4)
                throw new ArgumentException("Mal-formed WKT, wrong number of elements, expecting x y z m");

            var point = (IPoint)objectFactory.Create("esriGeometry.Point");
            //IPoint point = new PointClass();

            point.PutCoords(coordinates[0], coordinates[1]);

            if (wkt.HasZ)
                point.Z = coordinates[2];
            if (wkt.HasM && !wkt.HasZ)
                point.M = coordinates[2];
            if (wkt.HasZ && wkt.HasM)
                point.M = coordinates[3];

            MakeZmAware(point, wkt.HasZ, wkt.HasM);
            return point;
        }

        private static void MakeZmAware(IGeometry geometry, bool hasZ, bool hasM)
        {
            if (hasZ)
                ((IZAware)geometry).ZAware = true;
            if (hasM)
                ((IMAware)geometry).MAware = true;
        }

        #endregion
    }
}
