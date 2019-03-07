/*============================================================================== 
 Copyright (c) 2016-2017 PTC Inc. All Rights Reserved.
 
 Copyright (c) 2015 Qualcomm Connected Experiences, Inc. All Rights Reserved. 
 * ==============================================================================*/
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Vuforia;


public class UDTEventHandler : MonoBehaviour, IUserDefinedTargetEventHandler
{
    #region PUBLIC_MEMBERS
    /// <summary>
    /// Can be set in the Unity inspector to reference an ImageTargetBehaviour 
    /// that is instantiated for augmentations of new User-Defined Targets.
    /// </summary>
    public ImageTargetBehaviour ImageTargetTemplate;

    public int LastTargetIndex
    {
        get { return (m_TargetCounter - 1) % MAX_TARGETS; }
    }

	public string url = "https://vision.googleapis.com/v1/images:annotate?key=";
	public string apiKey = "AIzaSyBJD1-MC8zWO8XZ7FjBMkFh7Ufz1GR-YJc";
    #endregion PUBLIC_MEMBERS


    #region PRIVATE_MEMBERS
    const int MAX_TARGETS = 5;
    UserDefinedTargetBuildingBehaviour m_TargetBuildingBehaviour;
    QualityDialog m_QualityDialog;
    ObjectTracker m_ObjectTracker;
    TrackableSettings m_TrackableSettings;
    FrameQualityMeter m_FrameQualityMeter;
	private bool mShowGUIRect = false;

    // DataSet that newly defined targets are added to
    DataSet m_UDT_DataSet;

    // Currently observed frame quality
    ImageTargetBuilder.FrameQuality m_FrameQuality = ImageTargetBuilder.FrameQuality.FRAME_QUALITY_NONE;

    // Counter used to name newly created targets
    int m_TargetCounter;
    #endregion //PRIVATE_MEMBERS

	[System.Serializable]
	public class AnnotateImageRequests {
		public List<AnnotateImageRequest> requests;
	}

	[System.Serializable]
	public class AnnotateImageRequest {
		public Image image;
		public List<Feature> features;
	}

	[System.Serializable]
	public class Feature {
		public string type;
		public int maxResults;
	}

	[System.Serializable]
	public class ImageContext {
		public LatLongRect latLongRect;
		public List<string> languageHints;
	}

	[System.Serializable]
	public class LatLongRect {
		public LatLng minLatLng;
		public LatLng maxLatLng;
	}

	[System.Serializable]
	public class AnnotateImageResponses {
		public List<AnnotateImageResponse> responses;
	}

	[System.Serializable]
	public class AnnotateImageResponse {
		public List<FaceAnnotation> faceAnnotations;
		public List<EntityAnnotation> landmarkAnnotations;
		public List<EntityAnnotation> logoAnnotations;
		public List<EntityAnnotation> labelAnnotations;
		public List<EntityAnnotation> textAnnotations;
	}

	[System.Serializable]
	public class FaceAnnotation {
		public BoundingPoly boundingPoly;
		public BoundingPoly fdBoundingPoly;
		public List<Landmark> landmarks;
		public float rollAngle;
		public float panAngle;
		public float tiltAngle;
		public float detectionConfidence;
		public float landmarkingConfidence;
		public string joyLikelihood;
		public string sorrowLikelihood;
		public string angerLikelihood;
		public string surpriseLikelihood;
		public string underExposedLikelihood;
		public string blurredLikelihood;
		public string headwearLikelihood;
	}

	[System.Serializable]
	public class Landmark {
		public string type;
		public Position position;
	}

	[System.Serializable]
	public class BoundingPoly {
		public List<Vertex> vertices;
	}

	public class RectCoords{
		public List<Vertex> vertices;
	}

	private RectCoords rect1;

	[System.Serializable]
	public class Property {
		string name;
		string value;
	}

	[System.Serializable]
	public class EntityAnnotation {
		public string mid;
		public string locale;
		public string description;
		public float score;
		public float confidence;
		public float topicality;
		public BoundingPoly boundingPoly;
		public List<LocationInfo> locations;
		public List<Property> properties;
	}
	[System.Serializable]
	public class Position {
		public float x;
		public float y;
		public float z;
	}

	[System.Serializable]
	public class Vertex {
		public float x;
		public float y;
	}

	[System.Serializable]
	public class LocationInfo {
		LatLng latLng;
	}

	[System.Serializable]
	public class LatLng {
		float latitude;
		float longitude;
	}

	[System.Serializable]
	public class Image {
		public string content;
		public ImageSource source;
	}

	[System.Serializable]
	public class ImageSource{
		public string imageUri;
	}

	public int maxResults = 10;
	public FeatureType featureType = FeatureType.TEXT_DETECTION;
	public enum FeatureType {
		TYPE_UNSPECIFIED,
		FACE_DETECTION,
		LANDMARK_DETECTION,
		LOGO_DETECTION,
		LABEL_DETECTION,
		TEXT_DETECTION,
		SAFE_SEARCH_DETECTION,
		IMAGE_PROPERTIES
	}

	[System.Serializable]
	public class Currency {
		public bool success;
		public string terms;
		public string privacy;
		public float timestamp;
		public string source;
		public Quotes quotes;

	}
	[System.Serializable]
	public class Quotes {

		public float USDEUR;
		public float USDGBP;
		public float USDCAD;
		public float USDINR;
		public float USDHKD;
		public float USDJPY;
	}

    #region MONOBEHAVIOUR_METHODS
    void Start()
    {
        m_TargetBuildingBehaviour = GetComponent<UserDefinedTargetBuildingBehaviour>();

        if (m_TargetBuildingBehaviour)
        {
            m_TargetBuildingBehaviour.RegisterEventHandler(this);
            Debug.Log("Registering User Defined Target event handler.");
        }

        m_FrameQualityMeter = FindObjectOfType<FrameQualityMeter>();
        m_TrackableSettings = FindObjectOfType<TrackableSettings>();
        m_QualityDialog = FindObjectOfType<QualityDialog>();

        if (m_QualityDialog)
        {
            m_QualityDialog.GetComponent<CanvasGroup>().alpha = 0;
        }
    }
    #endregion //MONOBEHAVIOUR_METHODS


    #region IUserDefinedTargetEventHandler Implementation
    /// <summary>
    /// Called when UserDefinedTargetBuildingBehaviour has been initialized successfully
    /// </summary>
    public void OnInitialized()
    {
        m_ObjectTracker = TrackerManager.Instance.GetTracker<ObjectTracker>();
        if (m_ObjectTracker != null)
        {
            // Create a new dataset
            m_UDT_DataSet = m_ObjectTracker.CreateDataSet();
            m_ObjectTracker.ActivateDataSet(m_UDT_DataSet);
        }
    }

    /// <summary>
    /// Updates the current frame quality
    /// </summary>
    public void OnFrameQualityChanged(ImageTargetBuilder.FrameQuality frameQuality)
    {
        Debug.Log("Frame quality changed: " + frameQuality.ToString());
        m_FrameQuality = frameQuality;
        if (m_FrameQuality == ImageTargetBuilder.FrameQuality.FRAME_QUALITY_LOW)
        {
            Debug.Log("Low camera image quality");
        }

        m_FrameQualityMeter.SetQuality(frameQuality);
    }

    /// <summary>
    /// Takes a new trackable source and adds it to the dataset
    /// This gets called automatically as soon as you 'BuildNewTarget with UserDefinedTargetBuildingBehaviour
    /// </summary>
    public void OnNewTrackableSource(TrackableSource trackableSource)
    {
        m_TargetCounter++;

        // Deactivates the dataset first
        m_ObjectTracker.DeactivateDataSet(m_UDT_DataSet);

        // Destroy the oldest target if the dataset is full or the dataset 
        // already contains five user-defined targets.
        if (m_UDT_DataSet.HasReachedTrackableLimit() || m_UDT_DataSet.GetTrackables().Count() >= MAX_TARGETS)
        {
            IEnumerable<Trackable> trackables = m_UDT_DataSet.GetTrackables();
            Trackable oldest = null;
            foreach (Trackable trackable in trackables)
            {
                if (oldest == null || trackable.ID < oldest.ID)
                    oldest = trackable;
            }

            if (oldest != null)
            {
                Debug.Log("Destroying oldest trackable in UDT dataset: " + oldest.Name);
                m_UDT_DataSet.Destroy(oldest, true);
            }
        }

        // Get predefined trackable and instantiate it
        ImageTargetBehaviour imageTargetCopy = Instantiate(ImageTargetTemplate);
        imageTargetCopy.gameObject.name = "UserDefinedTarget-" + m_TargetCounter;

        // Add the duplicated trackable to the data set and activate it
        m_UDT_DataSet.CreateTrackable(trackableSource, imageTargetCopy.gameObject);

        // Activate the dataset again
        m_ObjectTracker.ActivateDataSet(m_UDT_DataSet);

        // Extended Tracking with user defined targets only works with the most recently defined target.
        // If tracking is enabled on previous target, it will not work on newly defined target.
        // Don't need to call this if you don't care about extended tracking.
        StopExtendedTracking();
        m_ObjectTracker.Stop();
        m_ObjectTracker.ResetExtendedTracking();
        m_ObjectTracker.Start();

        // Make sure TargetBuildingBehaviour keeps scanning...
        m_TargetBuildingBehaviour.StartScanning();
    }
    #endregion IUserDefinedTargetEventHandler implementation


    #region PUBLIC_METHODS
    /// <summary>
    /// Instantiates a new user-defined target and is also responsible for dispatching callback to 
    /// IUserDefinedTargetEventHandler::OnNewTrackableSource
    /// </summary>
    public void BuildNewTarget()
	{	Debug.Log("Build new target function called ");

		Vuforia.Image image = CameraDevice.Instance.GetCameraImage(Vuforia.Image.PIXEL_FORMAT.RGB888);

		if (image != null) {
			Debug.Log (
				"\nImage Format: " + image.PixelFormat +
				"\nImage Size:   " + image.Width + "x" + image.Height +
				"\nBuffer Size:  " + image.BufferWidth + "x" + image.BufferHeight +
				"\nImage Stride: " + image.Stride + "\n"
			);

			Texture2D tex = new Texture2D (image.Width, image.Height, TextureFormat.RGB24, false);
			image.CopyToTexture(tex);
			// tex.ReadPixels (new Rect(0, 0, image.Width, image.Height), 0, 0);
			tex.Apply ();

			byte[] bytes = tex.EncodeToPNG();
			Destroy(tex);

			// For testing purposes, also write to a file in the project folder
			File.WriteAllBytes(Application.persistentDataPath + "/appscreenshot.png", bytes);


			IEnumerator coroutine;
			coroutine = Callapi();
			StartCoroutine(coroutine);


		} else {
			
			Debug.Log ("unfortunately image is null");
		}
        if (m_FrameQuality == ImageTargetBuilder.FrameQuality.FRAME_QUALITY_MEDIUM ||
            m_FrameQuality == ImageTargetBuilder.FrameQuality.FRAME_QUALITY_HIGH)
        {
            // create the name of the next target.
            // the TrackableName of the original, linked ImageTargetBehaviour is extended with a continuous number to ensure unique names
            string targetName = string.Format("{0}-{1}", ImageTargetTemplate.TrackableName, m_TargetCounter);

            // generate a new target:
            m_TargetBuildingBehaviour.BuildNewTarget(targetName, ImageTargetTemplate.GetSize().x);
        }
        else
        {
            Debug.Log("Cannot build new target, due to poor camera image quality");
            if (m_QualityDialog)
            {
                StopAllCoroutines();
                m_QualityDialog.GetComponent<CanvasGroup>().alpha = 1;
                StartCoroutine(FadeOutQualityDialog());
            }
        }
    }



    #endregion //PUBLIC_METHODS


    #region PRIVATE_METHODS

	IEnumerator Callapi(){

		ScreenCapture.CaptureScreenshot("UnityScreenshot.png");
		yield return new WaitForSeconds(1.0f);
		Debug.Log ("Waited for 1 second");
		string url_ss = Application.persistentDataPath +"/UnityScreenshot.png";
		byte[] imgbytes = File.ReadAllBytes(url_ss);
		string base64 = System.Convert.ToBase64String(imgbytes);
		Debug.Log ("After saving image");

		AnnotateImageRequests requests = new AnnotateImageRequests();
		requests.requests = new List<AnnotateImageRequest>();

		AnnotateImageRequest request = new AnnotateImageRequest();
		request.image = new Image();

		ImageSource imageSrc = new ImageSource ();
		//imageSrc.imageUri = "https://storage.googleapis.com/archanababurajendra/img_recog_sample2.jpg";
		//request.image.source.imageUri = "https://storage.googleapis.com/archanababurajendra/img_recog_sample2.jpg";
		request.image.content = base64;
		//request.image.source = imageSrc;
		request.features = new List<Feature>();


		Feature feature = new Feature();
		feature.type = this.featureType.ToString();
		feature.maxResults = this.maxResults;

		request.features.Add(feature); 

		requests.requests.Add(request);

		string jsonData = JsonUtility.ToJson(requests, false);

		if (jsonData != string.Empty) {
			string url = this.url + this.apiKey;
			byte[] postData = System.Text.Encoding.Default.GetBytes(jsonData);

			Debug.Log ("In callapi method");

			var uwr = new UnityWebRequest (url, "POST");
			//byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(json);
			uwr.uploadHandler = (UploadHandler)new UploadHandlerRaw (postData);
			uwr.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer ();
			uwr.SetRequestHeader ("Content-Type", "application/json");

			//Send the request then wait here until it returns
			yield return uwr.SendWebRequest();

			if (uwr.isNetworkError) {
				Debug.Log ("Error While Sending: " + uwr.error);
			} else {
				Debug.Log ("Received: " + uwr.downloadHandler.text);
				AnnotateImageResponses responses = JsonUtility.FromJson<AnnotateImageResponses> (uwr.downloadHandler.text);
				Sample_OnAnnotateImageResponses(responses);
			}



		}





	}

	private void showMatch(string text, string expr) {
		Debug.Log("The Expression: " + expr);
		MatchCollection mc = Regex.Matches(text, expr);

		foreach (Match m in mc) {
			Debug.Log(m);
			string curr = m.ToString().Substring(1);
			float value = float.Parse(curr);
			//float value =(float) curr.Substring (1);
			IEnumerator coroutine;
			coroutine = callCurrencyApi(value);
			StartCoroutine(coroutine);

		}
	}

	private IEnumerator callCurrencyApi(float value){
		string url = "http://apilayer.net/api/live?access_key=91d993035856f000b4b918bf293ca711&currencies=EUR,GBP,CAD,INR,HKD,JPY&source=USD&format=1";
		UnityWebRequest uwr = UnityWebRequest.Get(url);

		uwr.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
		uwr.SetRequestHeader("Content-Type", "application/json");
		yield return uwr.SendWebRequest();

		if (uwr.isNetworkError)
		{
			Debug.Log("Error While Sending: " + uwr.error);
		}
		else
		{
			Debug.Log("Received in callCurrencyApi: " + uwr.downloadHandler.text);
			//float rupeeVal = uwr.downloadHandler.text;
			//JSONObject json = new JSONObject(uwr.downloadHandler.text);
			Currency currency = JsonUtility.FromJson<Currency>(uwr.downloadHandler.text);
			Debug.Log("Value "+value);
			Debug.Log("JSON -- "+currency.terms);
			Debug.Log("Rate -- "+currency.quotes.USDINR);
			Debug.Log("Final value -- "+ (value * currency.quotes.USDINR));
			//AnnotateImageResponses responses = JsonUtility.FromJson<AnnotateImageResponses>(webRequest.downloadHandler.text);
			//Sample_OnAnnotateImageResponses(responses);

		}
	}

	Vector3 ScreenToWorld(float x, float y) {
		Camera camera = Camera.current;
		Vector3 s = camera.WorldToScreenPoint(transform.position);
		return camera.ScreenToWorldPoint(new Vector3(x, camera.pixelHeight - y, s.z));
	}

	Rect ScreenRect(int x, int y, int w, int h) {
		Vector3 tl = ScreenToWorld(x, y);
		Vector3 br = ScreenToWorld(x + w, y + h);
		return new Rect(tl.x, tl.y, br.x - tl.x, br.y - tl.y);
	}

	void Sample_OnAnnotateImageResponses(AnnotateImageResponses responses) {
		Debug.Log("In annotate image responses method");
		if (responses.responses.Count > 0) {
			if (responses.responses[0].textAnnotations != null && responses.responses[0].textAnnotations.Count > 0) {

				foreach (var text in responses.responses[0].textAnnotations)
				{
					Debug.Log("ACTUAL TEXT " + text.description);
					//showMatch(text.description,@"(\D)\s*([.\d,]+)");


					if (text.description.StartsWith("$")) 
					{
						Debug.Log ("Found string starting with $  " + text.description);
						mShowGUIRect = true;
						int i=0;
						rect1 = new RectCoords ();
						rect1.vertices = new List<Vertex>();
						foreach (var vertex in text.boundingPoly.vertices) {

							//rect1.vertices = new List<Vertex>();

							if (i % 2 != 0) {
								Vertex a = new Vertex ();
								a.x = vertex.x;
								a.y = vertex.y;
								rect1.vertices.Add (a);

								Debug.Log ("dollar added POLY X  " + vertex.x);
								Debug.Log ("dollar added POLY Y  " + vertex.y);
							}
							i++;
						}

					}




				}

				

			}
		}
	}

	void OnGUI()

	{	if(mShowGUIRect){
			
			Debug.Log("ONGUI method");
			//Rect rect = ScreenRect((int)rect1.vertices[0].x, (int)rect1.vertices[0].y,(int) rect1.vertices[1].x, (int)rect1.vertices[1].y);
			Texture sometexture;
			sometexture = (Texture) Resources.Load("texture1");
			//Debug.Log ("before drawing rectangle");
			//GUI.DrawTexture (new Rect ((int)rect1.vertices[0].x, (int)rect1.vertices[0].y, (int) rect1.vertices[1].x, (int)rect1.vertices[1].y), sometexture );
			//Debug.Log("Coord 1 " + (int)rect1.vertices[0].x);
			//Debug.Log("Coord 2 " + (int)rect1.vertices [0].y);
			//Debug.Log("Coord 3 " + (int)rect1.vertices[1].x);
			//Debug.Log("Coord 4 " + (int)rect1.vertices[1].y);

			int coord1 = (int)rect1.vertices [0].x;
			int coord2 = (int)rect1.vertices [0].y;
			int coord3 = (int)rect1.vertices[1].x;
			int coord4 = (int)rect1.vertices[1].y;
			GUI.DrawTexture (new Rect (coord1,coord2,coord3,coord4), sometexture );
			//Debug.Log ("after drawing rectangle");
		}
	}

    IEnumerator FadeOutQualityDialog()
    {
        yield return new WaitForSeconds(1f);
        CanvasGroup canvasGroup = m_QualityDialog.GetComponent<CanvasGroup>();

        for (float f = 1f; f >= 0; f -= 0.1f)
        {
            f = (float)Math.Round(f, 1);
            Debug.Log("FadeOut: " + f);
            canvasGroup.alpha = (float)Math.Round(f, 1);
            yield return null;
        }
    }

    /// <summary>
    /// This method only demonstrates how to handle extended tracking feature when you have multiple targets in the scene
    /// So, this method could be removed otherwise
    /// </summary>
    void StopExtendedTracking()
    {
        // If Extended Tracking is enabled, we first disable it for all the trackables
        // and then enable it only for the newly created target
        bool extTrackingEnabled = m_TrackableSettings && m_TrackableSettings.IsExtendedTrackingEnabled();
        if (extTrackingEnabled)
        {
            StateManager stateManager = TrackerManager.Instance.GetStateManager();

            // 1. Stop extended tracking on all the trackables
            foreach (var tb in stateManager.GetTrackableBehaviours())
            {
                var itb = tb as ImageTargetBehaviour;
                if (itb != null)
                {
                    itb.ImageTarget.StopExtendedTracking();
                }
            }

            // 2. Start Extended Tracking on the most recently added target
            List<TrackableBehaviour> trackableList = stateManager.GetTrackableBehaviours().ToList();
            ImageTargetBehaviour lastItb = trackableList[LastTargetIndex] as ImageTargetBehaviour;
            if (lastItb != null)
            {
                if (lastItb.ImageTarget.StartExtendedTracking())
                    Debug.Log("Extended Tracking successfully enabled for " + lastItb.name);
            }
        }
    }

    #endregion //PRIVATE_METHODS
}
