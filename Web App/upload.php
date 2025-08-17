<?php
header("Content-Type: application/json");

$targetDir = "uploads/";

if (!file_exists($targetDir)) {
    mkdir($targetDir, 0777, true);
}

if (!isset($_FILES["image"])) {
    echo json_encode(["success" => false, "error" => "No file uploaded"]);
    exit;
}

$fileName = time() . "_" . basename($_FILES["image"]["name"]);
$targetFile = $targetDir . $fileName;

if (move_uploaded_file($_FILES["image"]["tmp_name"], $targetFile)) {
    echo json_encode(["success" => true, "filename" => $fileName]);
} else {
    echo json_encode(["success" => false, "error" => "Failed to save file"]);
}
?>
