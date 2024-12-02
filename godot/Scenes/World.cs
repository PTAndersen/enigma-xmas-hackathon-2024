using Godot;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

public class World : Spatial
{
	private Sprite3D Reindeer;
	private Sprite3D Destination;
	private Camera UserCamera;

	private bool _dragging = false;
	private bool _isStarted = false;
	private Vector3 _currentSphericalCoords;
	private Vector3 _targetSphericalCoords;
	private float _sphericalRadius = 3.5f;
	private float _lerpSpeed = 5f;

	private float _objectRadius = 1.5f;

	private float _reindeerSpeedKmPerSecond = 1.1f;
	private bool _targetReached = false;

	private string _message;
	private string _encryptedMessage;
	private string _destination;

	private static readonly byte[] Key = Encoding.UTF8.GetBytes("1234567890123456"); // Use a 16, 24, or 32-byte key
	private static readonly byte[] IV = Encoding.UTF8.GetBytes("1234567890123456");  // Use a 16-byte IV

	public override void _Ready()
	{
		Reindeer = GetNode<Sprite3D>("ReindeerSprite3D");
		Destination = GetNode<Sprite3D>("DestinationSprite3D2");
		UserCamera = GetNode<Camera>("Camera");

		_currentSphericalCoords = new Vector3(_sphericalRadius, Mathf.Pi / 2, 0);
		_targetSphericalCoords = _currentSphericalCoords;

		SetSpritePosition(Reindeer, 90.0f, 0.0f); // North Pole
		SetSpritePosition(Destination, -90.0f, 0.0f); // South Pole



		_destination = "-90,0";
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseEvent)
		{
			if (mouseEvent.ButtonIndex == (int)ButtonList.Left)
			{
				_dragging = mouseEvent.Pressed;
			}
		}
		else if (@event is InputEventMouseMotion motionEvent && _dragging)
		{
			Vector2 mouseDelta = motionEvent.Relative;

			// Adjust the latitude (theta) and longitude (phi) based on mouse movement
			_targetSphericalCoords.y -= mouseDelta.y * 0.005f; // Latitude (up/down)
			_targetSphericalCoords.z -= mouseDelta.x * 0.005f; // Longitude (left/right)

			// Clamp latitude to avoid flipping at poles
			_targetSphericalCoords.y = Mathf.Clamp(_targetSphericalCoords.y, 0.1f, Mathf.Pi - 0.1f);
		}
	}

	public override void _Process(float delta)
	{
		if (_dragging)
		{
			_currentSphericalCoords = new Vector3(
				_sphericalRadius,
				Mathf.Lerp(_currentSphericalCoords.y, _targetSphericalCoords.y, _lerpSpeed * delta),
				Mathf.Lerp(_currentSphericalCoords.z, _targetSphericalCoords.z, _lerpSpeed * delta)
			);

			UpdateCameraPosition();
		}

		if (_isStarted && !_targetReached)
		{
			MoveReindeerTowardsDestination(delta);
			CheckIfTargetReached();
		}
	}

	private void CheckIfTargetReached()
	{
		float distance = Reindeer.Translation.DistanceTo(Destination.Translation);

		if (distance < 0.01f)
		{
			_targetReached = true;
			OnReindeerReachedTarget();
		}
	}

	private string EncryptMessage(string message, string location)
	{
		GD.Print(location);
		string combinedMessage = $"{message}|{location}";
		using (Aes aes = Aes.Create())
		{
			aes.Key = Key;
			aes.IV = IV;

			ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

			using (MemoryStream ms = new MemoryStream())
			{
				using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
				{
					using (StreamWriter sw = new StreamWriter(cs))
					{
						sw.Write(combinedMessage);
					}
				}
				return Convert.ToBase64String(ms.ToArray());
			}
		}
	}

	private void OnReindeerReachedTarget()
	{
		_encryptedMessage = EncryptMessage(_message, _destination);

		PanelContainer panelContainer = GetNode<PanelContainer>("CanvasLayer/Control/PanelContainer3");
		panelContainer.Visible = true;
		TextEdit textEdit = GetNode<TextEdit>("CanvasLayer/Control/PanelContainer3/VBox/TextEdit");
		textEdit.Text = _encryptedMessage;
	}



	private void UpdateCameraPosition()
	{
		float x = _sphericalRadius * Mathf.Sin(_currentSphericalCoords.y) * Mathf.Cos(_currentSphericalCoords.z);
		float y = _sphericalRadius * Mathf.Cos(_currentSphericalCoords.y);
		float z = _sphericalRadius * Mathf.Sin(_currentSphericalCoords.y) * Mathf.Sin(_currentSphericalCoords.z);

		Vector3 newPosition = new Vector3(x, y, z);

		UserCamera.Translation = newPosition;

		UserCamera.LookAt(Vector3.Zero, Vector3.Up);
	}

	public void SetSpritePosition(Sprite3D sprite, float latitude, float longitude)
	{
		float latRad = Mathf.Deg2Rad(latitude);
		float lonRad = Mathf.Deg2Rad(longitude);

		float x = _objectRadius * Mathf.Cos(latRad) * Mathf.Cos(lonRad);
		float y = _objectRadius * Mathf.Sin(latRad);
		float z = _objectRadius * Mathf.Cos(latRad) * Mathf.Sin(lonRad);

		sprite.Translation = new Vector3(x, y, z);
	}

	private void MoveReindeerTowardsDestination(float delta)
	{
		Vector3 reindeerCartesian = Reindeer.Translation.Normalized();
		Vector3 destinationCartesian = Destination.Translation.Normalized();

		Vector3 rotationAxis = reindeerCartesian.Cross(destinationCartesian).Normalized();

		if (rotationAxis.Length() < 0.001f)
		{
			return;
		}

		float dot = reindeerCartesian.Dot(destinationCartesian);
		dot = Mathf.Clamp(dot, -1.0f, 1.0f);
		float angularDistance = Mathf.Acos(dot);

		float stepRadians = (_reindeerSpeedKmPerSecond / _objectRadius) * delta;

		if (stepRadians > angularDistance)
		{
			stepRadians = angularDistance;
		}

		Vector3 newPosition = reindeerCartesian.Rotated(rotationAxis, stepRadians).Normalized() * _objectRadius;
		Reindeer.Translation = newPosition;
	}

	private void _on_LocationLineEdit_text_entered(String new_text)
	{
		string[] parts = new_text.Split(',');
		if (parts.Length == 2 && float.TryParse(parts[0], out float latitude) && float.TryParse(parts[1], out float longitude))
		{
			SetSpritePosition(Reindeer, latitude, longitude);
		}
	}

	private void _on_DestinationLineEdit_text_entered(String new_text)
	{
		_destination = new_text;

		string[] parts = new_text.Split(',');
		if (parts.Length == 2 && float.TryParse(parts[0], out float latitude) && float.TryParse(parts[1], out float longitude))
		{
			SetSpritePosition(Destination, latitude, longitude);
		}
	}

	private void _on_StartButton_pressed()
	{
		_isStarted = true;
	}

	private void _on_SubmitButton_pressed()
	{
		TextEdit textEdit = GetNode<TextEdit>("CanvasLayer/Control/PanelContainer2/VBox/TextEdit");
		_message = textEdit.Text;
		
		PanelContainer panelContainer = GetNode<PanelContainer>("CanvasLayer/Control/PanelContainer2");
		panelContainer.Visible = false;
	}

	
}


