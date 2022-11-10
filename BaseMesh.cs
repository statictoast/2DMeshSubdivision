using System.Collections.Generic;
using UnityEngine;
using MathUtils;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class BaseMesh : MonoBehaviour
{
    private Vector3[] vertices;
    private Vector2[] uvs;
    private Vector4[] tangents;
    private Mesh mesh;
    private MeshRenderer meshRenderer;

    public int GetNumVerticies()
    {
        return vertices.Length;
    }

    public Vector3 GetVertex(int aIndex)
    {
        return vertices[aIndex];
    }

    public int FindVertexByPoint(Vector3 aVertex)
    {
        for(int i = 0; i < vertices.Length; i++)
        {
            if(vertices[i] == aVertex)
            {
                return i;
            }
        }

        return -1;
    }

    public Vector4 GetVerticeUVPair(int aIndex)
    {
        return new Vector4(vertices[aIndex].x, vertices[aIndex].y, uvs[aIndex].x, uvs[aIndex].y);
    }

    // http://www.geeksforgeeks.org/convex-hull-set-2-graham-scan/
    public Vector3[] GenerateConvexHull()
    {
        List<Vector3> convexHullPoints = new List<Vector3>();

        List<Vector3> verticiesByPolar = new List<Vector3>(vertices);
        Vector3 firstPoint = verticiesByPolar[0];
        verticiesByPolar.RemoveAt(0);
        verticiesByPolar.Sort(delegate(Vector3 aLeft, Vector3 aRight)
                              {
                                  // Find orientation
                                  THREE_POINT_ORIENTATION orientation = MathUtil.GetPointsOrientation(firstPoint, aLeft, aRight);
                                  switch(orientation)
                                  {
                                        case THREE_POINT_ORIENTATION.COLINEAR:
                                        {
                                            float differenceInDistance = Vector3.Distance(firstPoint, aRight) - Vector3.Distance(firstPoint, aLeft);
                                            if(differenceInDistance > 0)
                                            {
                                                return -1;
                                            }
                                            else if(differenceInDistance < 0)
                                            {
                                                return 1;
                                            }

                                            return 0;
                                        }
                                        case THREE_POINT_ORIENTATION.COUNTER_CLOCKWISE:
                                        { 
                                            return -1;
                                        }
                                        case THREE_POINT_ORIENTATION.CLOCKWISE:
                                        {
                                            return 1;
                                        }
                                  }
                                  return 0;
                              });

        convexHullPoints.Add(firstPoint);
        convexHullPoints.Add(verticiesByPolar[0]);
        convexHullPoints.Add(verticiesByPolar[1]);

        for (int i = 2; i < verticiesByPolar.Count; i++)
        {
            Vector3 currentPoint = verticiesByPolar[i];
            while (MathUtil.GetPointsOrientation(convexHullPoints[convexHullPoints.Count - 2], convexHullPoints[convexHullPoints.Count - 1], currentPoint) != THREE_POINT_ORIENTATION.COUNTER_CLOCKWISE)
            {
                convexHullPoints.RemoveAt(convexHullPoints.Count - 1);
                if(convexHullPoints.Count < 2)
                {
                    Debug.LogError("failure in BaseMesh::GenerateConvexHull, not enough points in our hull.");
                    break;
                }
            }
            convexHullPoints.Add(currentPoint);
        }

        return convexHullPoints.ToArray();
    }

    public bool DoesLineCauseSubdivide(LineSegment aLineSegment, ref Vector2[] aIntersectionPoints, ref Vector2[] aCalculatedUVs)
    {
        Vector3[] convexHullPoints = GenerateConvexHull();
        int numPoints = convexHullPoints.Length;
        int foundPoints = 0;
        Vector3[] pointsInSegment = aLineSegment.GetLinesAsArray();

        for (int i = 0; i < numPoints; i++)
        {
            Vector2 intersectionPoint;
            Vector3 sidePoint1 = convexHullPoints[i];
            Vector3 sidePoint2 = convexHullPoints[(i + 1) % numPoints];
            if (MathUtil.ComputeLinesIntersect(sidePoint1, sidePoint2, pointsInSegment[0], pointsInSegment[1], out intersectionPoint))
            {
                // check for special case where intersection point is a point in the hull
                // in this case, we will get 2 points that are the same position
                bool foundDupe = false;
                for (int j = 0; j < foundPoints; ++j)
                {
                    if (aIntersectionPoints[j] == intersectionPoint)
                    {
                        foundDupe = true;
                        break;
                    }
                }

                if (!foundDupe)
                {
                    if (foundPoints >= 2)
                    {
                        Debug.LogError("Calculated a polygon/line intersection in more than 2 places");
                    }
                    else
                    {
                        // need to find the UV points now:
                        // lerp - min / (max - min)
                        float percentage = Vector2.Distance(intersectionPoint, sidePoint1) / Vector2.Distance(sidePoint2, sidePoint1);
                        int vertPos1 = FindVertexByPoint(sidePoint1);
                        int vertPos2 = FindVertexByPoint(sidePoint2);
                        Vector2 uv1 = uvs[vertPos1];
                        Vector2 uv2 = uvs[vertPos2];
                        Vector2 lerpUV = Vector2.Lerp(uv1, uv2, percentage);

                        aIntersectionPoints[foundPoints] = intersectionPoint;
                        aCalculatedUVs[foundPoints] = lerpUV;
                        foundPoints++;
                    }
                }
            }
        }

        return foundPoints == 2;
    }

    public void SetMaterial(Material aMaterial)
    {
        if(meshRenderer == null)
        {
            meshRenderer = GetComponent<MeshRenderer>();
        }
        meshRenderer.material = aMaterial;
    }

    // Left to Right, Bottom to Top
    public static int SortVerticiesByHorizontalAscension(Vector4 aLeft, Vector4 aRight)
    {
        if (aLeft.x < aRight.x)
        {
            return -1;
        }
        else if (aLeft.x > aRight.x)
        {
            return 1;
        }
        else
        {
            if (aLeft.y < aRight.y)
            {
                return -1;
            }
            else if (aLeft.y > aRight.y)
            {
                return 1;
            }
        }
        return 0;
    }

    // Bottom to Top, Left to Right
    protected static int SortVerticiesByVerticalAscension(Vector4 aLeft, Vector4 aRight)
    {
        if (aLeft.y == aRight.y)
        {
            if (aLeft.x < aRight.x)
            {
                return -1;
            }
            else if (aLeft.x > aRight.x)
            {
                return 1;
            }
        }
        else
        {
            if (aLeft.y < aRight.y)
            {
                return -1;
            }
            else if (aLeft.y > aRight.y)
            {
                return 1;
            }
        }
        return 0;
    }

    // Check number of coordinates and pass to
    // more efficient functions based on special cases
    /**
     * <summary>Takes a number of vertices and creates a mesh out of them. 
     * Picks the most performant way to create a mesh out of them based on number of vertices passed in.
     * Coords are assumed sorted Left to Right, Bottom to Top.</summary>
     * <param name="aCoords">x and y are the coordinates in the x,y plane. z and w and are uv coords of the point (x,y).</param>
     */
    public void Generate2DMesh(List<Vector4> aCoords)
    {
        int numCoords = aCoords.Count;
        Debug.Assert(numCoords > 2, "Tryed to create a 2d mesh with less than 3 points");

        if (numCoords == 3)
        {
            // only 1 way to make 3 points into a triangle
            Generate2DTriangleMesh(aCoords.ToArray());
            return;
        }

        aCoords.Sort(SortVerticiesByHorizontalAscension);
        // since the generic polygon function is the least performant,
        // it is the default case if we can't find a triangle or square
        Generate2DPolygonMesh(aCoords.ToArray());
    }

    #region QuadMesh
    private void Generate2DQaudMesh(Vector4[] aQuadCords)
    {
        Debug.Assert(aQuadCords.Length == 4, "tried to pass an array of not 4 points to Generate2DQaudMesh");

        GetComponent<MeshFilter>().mesh = mesh = new Mesh();
        mesh.name = "Procedural Quad";

        vertices = new Vector3[4];
        uvs = new Vector2[vertices.Length];
        tangents = new Vector4[vertices.Length];
        Vector4 tangent = new Vector4(1f, 0f, 0f, -1f);

        for(int i = 0; i < aQuadCords.Length; i++)
        {
            vertices[i] = new Vector3(aQuadCords[i].x, aQuadCords[i].y);
            uvs[i] = new Vector2(aQuadCords[i].z, aQuadCords[i].w);
            tangents[i] = tangent;
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.tangents = tangents;

        GenerateQuadTrinagles();
    }

    private void GenerateQuadTrinagles()
    {
        int[] triangles = new int[6];
        for (int ti = 0, vi = 0, y = 0; y < 1; y++, vi++)
        {
            for (int x = 0; x < 1; x++, ti += 6, vi++)
            {
                triangles[ti] = vi;
                triangles[ti + 3] = triangles[ti + 2] = vi + 2;
                triangles[ti + 4] = triangles[ti + 1] = vi + 1;
                triangles[ti + 5] = vi + 3;
            }
        }

        mesh.triangles = triangles;
        mesh.RecalculateNormals();
    }
    #endregion QuadMesh

    #region TriangleMesh
    private void Generate2DTriangleMesh(Vector4[] aTriangleCords)
    {
        Debug.Assert(aTriangleCords.Length == 3, "tried to pass an array of not 3 points to Generate2DTriangleMesh");

        GetComponent<MeshFilter>().mesh = mesh = new Mesh();
        mesh.name = "Procedural Triangle";

        vertices = new Vector3[3];
        uvs = new Vector2[vertices.Length];
        tangents = new Vector4[vertices.Length];
        Vector4 tangent = new Vector4(1f, 0f, 0f, -1f);

        for(int i = 0; i < aTriangleCords.Length; i++)
        {
            vertices[i] = new Vector3(aTriangleCords[i].x, aTriangleCords[i].y);
            uvs[i] = new Vector2(aTriangleCords[i].z, aTriangleCords[i].w);
            tangents[i] = tangent;
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.tangents = tangents;

        GenerateSingleTriangle();
    }

    private void GenerateSingleTriangle()
    {
        int[] triangles = new int[3];
        triangles[0] = 0;
        triangles[1] = 1;
        triangles[2] = 2;

        mesh.triangles = triangles;
        mesh.RecalculateNormals();
    }
    #endregion TriangleMesh

    #region PolygonMesh
    private void Generate2DPolygonMesh(Vector4[] aCoords)
    {
        GetComponent<MeshFilter>().mesh = mesh = new Mesh();
        mesh.name = "Procedural Polygon";

        vertices = new Vector3[aCoords.Length];
        uvs = new Vector2[vertices.Length];
        tangents = new Vector4[vertices.Length];
        Vector4 tangent = new Vector4(1f, 0f, 0f, -1f);

        for(int i = 0; i < aCoords.Length; i++)
        {
            vertices[i] = new Vector3(aCoords[i].x, aCoords[i].y);
            uvs[i] = new Vector2(aCoords[i].z, aCoords[i].w);
            tangents[i] = tangent;
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.tangents = tangents;

        GeneratePolygonTriangles();
    }

    private void GeneratePolygonTriangles()
    {
        // Use favorite Triangulation method here
        // ---------------------------------------
        
        Triangle[] foundTriangles;
        float startTime = Time.realtimeSinceStartup;
        // internet found method
        DelaunayTriangulation.Instance.Triangulate(vertices, out foundTriangles);
        Debug.Log("End Triangulation: " + (Time.realtimeSinceStartup - startTime) * 1000 + " Ms");

        int numTrinaglesNeeded = foundTriangles.Length * 3;
        int[] triangles = new int[numTrinaglesNeeded];

        for (int i = 0, ti = 0; i < foundTriangles.Length; i++, ti += 3)
        {
            Triangle foundTriangle = foundTriangles[i];
            triangles[ti] = foundTriangle.GetVertex(0).vertexPosition;
            triangles[ti + 1] = foundTriangle.GetVertex(1).vertexPosition;
            triangles[ti + 2] = foundTriangle.GetVertex(2).vertexPosition;
        }

        mesh.triangles = triangles;
        mesh.RecalculateNormals();
    }
    #endregion PolygonMesh
}
