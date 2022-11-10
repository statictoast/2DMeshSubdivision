using System.Collections.Generic;
using UnityEngine;
using MathUtils;

/**
 * <summary> 
 * Attach to a GameObject to create a mesh that can be subdivided until multiple parts.
 * Calls to AddSlice will attempt to find the 2 sub meshes that would be created
 * on each side of the line, if 2 could exist, creating 2 GameObjects representing
 * each half of the original mesh.
 * </summary>
 */
public class SliceableMesh : MonoBehaviour
{
	public Sprite baseSprite;
	public string spriteShaderLocation = "Sprites/Default";

	protected Material spriteMaterial;

	protected List<LineSegment> slices;
	protected List<GameObject> meshObjects;

	private void Awake()
	{
		meshObjects = new List<GameObject>();
		slices = new List<LineSegment>();
		GenerateBaseSprite();
	}

	protected void GenerateBaseSprite()
	{
		if(baseSprite == null)
		{
			Debug.LogError("Trying to slice a sprite, but we have no sprite to slice, making a default quad");
			GenerateDefault();
			return;
		}

		Vector2[] meshVerts = baseSprite.vertices;
		Vector2[] meshUVs = baseSprite.uv;
		List<Vector4> vertsAndUVs = new List<Vector4>();
		for (int i = 0; i < meshVerts.Length; i++)
		{
			vertsAndUVs.Add(new Vector4(meshVerts[i].x, meshVerts[i].y, meshUVs[i].x, meshUVs[i].y));
		}

		spriteMaterial = new Material(Shader.Find(spriteShaderLocation));
		spriteMaterial.SetTexture("_MainTex", baseSprite.texture);
		GameObject firstMesh = CreateMeshGameObject();
		firstMesh.GetComponent<BaseMesh>().Generate2DMesh(vertsAndUVs);
	}

	// creates a default 1x1 quad
	private void GenerateDefault()
	{
		List<Vector4> quad = new List<Vector4>();
		int currentPoint = 0;
		for (int i = 0, y = 0; y <= 1; y++)
		{
			for (int x = 0; x <= 1; x++, i++)
			{
				float xPos = x;
				float yPos = y;
				quad.Add(new Vector4(xPos, yPos, xPos, yPos));
				currentPoint++;
			}
		}
		GameObject firstMesh = CreateMeshGameObject();
		firstMesh.GetComponent<BaseMesh>().Generate2DMesh(quad);
	}

	public void AddSlice(LineSegment aLineSegment)
	{
		slices.Add(aLineSegment);

		int numMeshObjects = meshObjects.Count;
		for (int i = 0; i < numMeshObjects; ++i)
		{
			GameObject nextMeshObject = meshObjects[i];
			BaseMesh baseMesh = nextMeshObject.GetComponent<BaseMesh>();
			Vector2[] intersectionPoints = new Vector2[2];
			Vector2[] calculatedUVs = new Vector2[2];
			if (!baseMesh.DoesLineCauseSubdivide(aLineSegment, ref intersectionPoints, ref calculatedUVs))
			{
				Debug.Log("[SliceableMesh::AddSlice] Given line segment does not intersect through mesh, attempt to extend the segment in both directions and try again");
				aLineSegment.ExtendPoints();
				if (!baseMesh.DoesLineCauseSubdivide(aLineSegment, ref intersectionPoints, ref calculatedUVs))
				{
					Debug.Log("[SliceableMesh::AddSlice] something bad has happened when adding a slice");
					slices.RemoveAt(slices.Count - 1);
					return;
				}
			}

			// subdivided mesh
			LineSegment intersectionLine = new LineSegment(intersectionPoints[0], intersectionPoints[1]);
			SubdivideMesh(nextMeshObject, intersectionLine, calculatedUVs);
		}
	}

	protected static bool AddUniqueVectorEntry(ref List<Vector4> aList, Vector4 aEntry)
	{
		foreach(Vector4 entry in aList)
		{
			if(Vector4.Distance(entry, aEntry) < 0.001f)
			{
				return false;
			}
		}
		aList.Add(aEntry);
		return true;
	}

	protected void SubdivideMesh(GameObject aMeshObject, LineSegment aIntersectionLine, Vector2[] aCalculatedUVs)
	{
		List<Vector4> leftVertices = new List<Vector4>();
		List<Vector4> rightVertices = new List<Vector4>();

		BaseMesh baseMesh = aMeshObject.GetComponent<BaseMesh>();
		int numMeshVerts = baseMesh.GetNumVerticies();
		for (int i = 0; i < numMeshVerts; ++i)
		{
			Vector4 nextVerticeUVPair = baseMesh.GetVerticeUVPair(i);
			int whichSide = aIntersectionLine.WhichSideOfLineIsPointOn(nextVerticeUVPair);
			if(whichSide > 0)
			{
				AddUniqueVectorEntry(ref rightVertices, nextVerticeUVPair);
			}
			else if(whichSide < 0)
			{
				AddUniqueVectorEntry(ref leftVertices, nextVerticeUVPair);
			}
			else
			{
				// point is on the line, so it goes on "both" sides of a line
				AddUniqueVectorEntry(ref rightVertices, nextVerticeUVPair);
				AddUniqueVectorEntry(ref leftVertices, nextVerticeUVPair);
			}
		}

		Vector4 intersectionPoint1 = new Vector4(aIntersectionLine.point1.x, aIntersectionLine.point1.y, aCalculatedUVs[0].x, aCalculatedUVs[0].y);
		Vector4 intersectionPoint2 = new Vector4(aIntersectionLine.point2.x, aIntersectionLine.point2.y, aCalculatedUVs[1].x, aCalculatedUVs[1].y);

		AddUniqueVectorEntry(ref leftVertices, intersectionPoint1);
		AddUniqueVectorEntry(ref leftVertices, intersectionPoint2);

		AddUniqueVectorEntry(ref rightVertices, intersectionPoint1);
		AddUniqueVectorEntry(ref rightVertices, intersectionPoint2);

		baseMesh.Generate2DMesh(leftVertices);
		GameObject newMesh = CreateMeshGameObject();
		newMesh.GetComponent<BaseMesh>().Generate2DMesh(rightVertices);
	}

	protected GameObject CreateMeshGameObject()
	{
		GameObject meshObject = new GameObject();
		meshObject.name = "BaseMesh" + (meshObjects.Count + 1);
		meshObject.transform.parent = this.transform;
		meshObject.transform.localPosition = new Vector3(); // perhaps incorrect
		meshObject.AddComponent<BaseMesh>();
		meshObject.GetComponent<BaseMesh>().SetMaterial(spriteMaterial);
		meshObjects.Add(meshObject);
		return meshObject;
	}

	private Color[] lineColors = { Color.green, Color.red, Color.magenta, Color.yellow, Color.blue, Color.white };

	protected void OnDrawGizmos()
	{
		if (slices == null)
		{
			return;
		}

		for (int i = 0; i < slices.Count; i++)
		{
			Gizmos.color = Color.black;
			Vector3[] newPoints = slices[i].GetLinesAsArray();
			for (int j = 0; j < 2; j++)
			{
				Gizmos.DrawSphere(transform.TransformPoint(newPoints[j]), 0.1f);
			}
			Gizmos.color = lineColors[i % lineColors.Length];
			Gizmos.DrawLine(transform.TransformPoint(newPoints[0]), transform.TransformPoint(newPoints[1]));
		}
	}
}
