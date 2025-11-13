using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenCvSharp;
using OpenCVGui.Lib;
using OpenCVGui.Lib.Serialization;
using OpenCVGui.Lib.Transform;
using OpenCVGui.Lib.Import;
using OpenCVGui.Lib.Contour;

namespace OpenCVGui.Lib.Serialization.Tests
{
    [TestClass()]
    public class LayerChainSerializerTests
    {
        [TestMethod()]
        public void Serialize_EmptyChain_ReturnsValidXml()
        {
            // Arrange
            var chain = new LayerChain();

            // Act
            var xml = LayerChainSerializer.Serialize(chain);

            // Assert
            Assert.IsNotNull(xml);
            Assert.IsTrue(xml.Contains("<LayerChain"));
            Assert.IsTrue(xml.Contains("</LayerChain>"));
        }

        [TestMethod()]
        public void Serialize_ChainWithSingleLayer_ReturnsValidXml()
        {
            // Arrange
            var chain = new LayerChain();
            var layer = new ImageZoomLayer
            {
                Name = "Test Zoom",
                ZoomFactor = 2.5
            };
            chain.AddLayer(layer);

            // Act
            var xml = LayerChainSerializer.Serialize(chain);

            // Assert
            Assert.IsNotNull(xml);
            Assert.IsTrue(xml.Contains("ImageZoom"));
            Assert.IsTrue(xml.Contains("Test Zoom"));
            Assert.IsTrue(xml.Contains("ZoomFactor"));
            Assert.IsTrue(xml.Contains("2.5"));
        }

        [TestMethod()]
        public void Serialize_ChainWithMultipleLayers_ReturnsValidXml()
        {
            // Arrange
            var chain = new LayerChain();
            
            var importLayer = new ImageImportLayer
            {
                Name = "Import",
                ImagePath = "test.png"
            };
            chain.AddLayer(importLayer);

            var zoomLayer = new ImageZoomLayer
            {
                Name = "Zoom",
                ZoomFactor = 1.5
            };
            chain.AddLayer(zoomLayer);

            var rotationLayer = new ImageRotationLayer
            {
                Name = "Rotation",
                Angle = 45.0,
                ExpandToFit = true
            };
            chain.AddLayer(rotationLayer);

            // Act
            var xml = LayerChainSerializer.Serialize(chain);

            // Assert
            Assert.IsNotNull(xml);
            Assert.IsTrue(xml.Contains("ImageImport"));
            Assert.IsTrue(xml.Contains("ImageZoom"));
            Assert.IsTrue(xml.Contains("ImageRotation"));
            Assert.IsTrue(xml.Contains("test.png"));
            Assert.IsTrue(xml.Contains("1.5"));
            Assert.IsTrue(xml.Contains("45"));
        }

        [TestMethod()]
        public void Serialize_Deserialize_RoundTripPreservesData()
        {
            // Arrange
            var originalChain = new LayerChain();
            
            var importLayer = new ImageImportLayer
            {
                Id = Guid.NewGuid(),
                Name = "Import Layer",
                ImagePath = "path/to/image.jpg",
                IsEnabled = true
            };
            originalChain.AddLayer(importLayer);

            var zoomLayer = new ImageZoomLayer
            {
                Id = Guid.NewGuid(),
                Name = "Zoom Layer",
                ZoomFactor = 2.0,
                Interpolation = InterpolationFlags.Cubic,
                IsEnabled = true
            };
            originalChain.AddLayer(zoomLayer);

            var rotationLayer = new ImageRotationLayer
            {
                Id = Guid.NewGuid(),
                Name = "Rotation Layer",
                Angle = 90.0,
                ExpandToFit = false,
                Interpolation = InterpolationFlags.Linear,
                IsEnabled = false
            };
            originalChain.AddLayer(rotationLayer);

            // Act
            var xml = LayerChainSerializer.Serialize(originalChain);
            var deserializedChain = LayerChainSerializer.Deserialize(xml);

            // Assert
            Assert.AreEqual(originalChain.Count, deserializedChain.Count);

            var originalImport = (ImageImportLayer)originalChain[0];
            var deserializedImport = (ImageImportLayer)deserializedChain[0];
            Assert.AreEqual(originalImport.Id, deserializedImport.Id);
            Assert.AreEqual(originalImport.Name, deserializedImport.Name);
            Assert.AreEqual(originalImport.ImagePath, deserializedImport.ImagePath);
            Assert.AreEqual(originalImport.IsEnabled, deserializedImport.IsEnabled);

            var originalZoom = (ImageZoomLayer)originalChain[1];
            var deserializedZoom = (ImageZoomLayer)deserializedChain[1];
            Assert.AreEqual(originalZoom.Id, deserializedZoom.Id);
            Assert.AreEqual(originalZoom.Name, deserializedZoom.Name);
            Assert.AreEqual(originalZoom.ZoomFactor, deserializedZoom.ZoomFactor);
            Assert.AreEqual(originalZoom.Interpolation, deserializedZoom.Interpolation);
            Assert.AreEqual(originalZoom.IsEnabled, deserializedZoom.IsEnabled);

            var originalRotation = (ImageRotationLayer)originalChain[2];
            var deserializedRotation = (ImageRotationLayer)deserializedChain[2];
            Assert.AreEqual(originalRotation.Id, deserializedRotation.Id);
            Assert.AreEqual(originalRotation.Name, deserializedRotation.Name);
            Assert.AreEqual(originalRotation.Angle, deserializedRotation.Angle);
            Assert.AreEqual(originalRotation.ExpandToFit, deserializedRotation.ExpandToFit);
            Assert.AreEqual(originalRotation.Interpolation, deserializedRotation.Interpolation);
            Assert.AreEqual(originalRotation.IsEnabled, deserializedRotation.IsEnabled);
        }

        [TestMethod()]
        public void Serialize_Deserialize_PreservesLayerOrder()
        {
            // Arrange
            var chain = new LayerChain();
            chain.AddLayer(new ImageImportLayer { Name = "First" });
            chain.AddLayer(new ImageZoomLayer { Name = "Second" });
            chain.AddLayer(new ImageRotationLayer { Name = "Third" });

            // Act
            var xml = LayerChainSerializer.Serialize(chain);
            var deserialized = LayerChainSerializer.Deserialize(xml);

            // Assert
            Assert.AreEqual(3, deserialized.Count);
            Assert.AreEqual("First", deserialized[0].Name);
            Assert.AreEqual("Second", deserialized[1].Name);
            Assert.AreEqual("Third", deserialized[2].Name);
        }

        [TestMethod()]
        public void Serialize_ContourDetectionLayer_PreservesAllParameters()
        {
            // Arrange
            var chain = new LayerChain();
            var contourLayer = new ContourDetectionLayer
            {
                Name = "Contour",
                MinArea = 150.0,
                MaxArea = 5000.0,
                DrawOnOriginal = false,
                ContourColorB = 255,
                ContourColorG = 0,
                ContourColorR = 0,
                LineThickness = 3,
                RetrievalMode = RetrievalModes.List,
                ApproximationMode = ContourApproximationModes.ApproxNone
            };
            chain.AddLayer(contourLayer);

            // Act
            var xml = LayerChainSerializer.Serialize(chain);
            var deserialized = LayerChainSerializer.Deserialize(xml);
            var deserializedContour = (ContourDetectionLayer)deserialized[0];

            // Assert
            Assert.AreEqual(contourLayer.MinArea, deserializedContour.MinArea);
            Assert.AreEqual(contourLayer.MaxArea, deserializedContour.MaxArea);
            Assert.AreEqual(contourLayer.DrawOnOriginal, deserializedContour.DrawOnOriginal);
            Assert.AreEqual(contourLayer.ContourColorB, deserializedContour.ContourColorB);
            Assert.AreEqual(contourLayer.ContourColorG, deserializedContour.ContourColorG);
            Assert.AreEqual(contourLayer.ContourColorR, deserializedContour.ContourColorR);
            Assert.AreEqual(contourLayer.LineThickness, deserializedContour.LineThickness);
            Assert.AreEqual(contourLayer.RetrievalMode, deserializedContour.RetrievalMode);
            Assert.AreEqual(contourLayer.ApproximationMode, deserializedContour.ApproximationMode);
        }

        [TestMethod()]
        public void Serialize_ColorConversionLayer_PreservesEnumParameter()
        {
            // Arrange
            var chain = new LayerChain();
            var colorLayer = new ColorConversion
            {
                Name = "Color Conv",
                Target = ColorConversionCodes.BGR2Lab
            };
            chain.AddLayer(colorLayer);

            // Act
            var xml = LayerChainSerializer.Serialize(chain);
            var deserialized = LayerChainSerializer.Deserialize(xml);
            var deserializedColor = (ColorConversion)deserialized[0];

            // Assert
            Assert.AreEqual(colorLayer.Target, deserializedColor.Target);
        }
    }
}