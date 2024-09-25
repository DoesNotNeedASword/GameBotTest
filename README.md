# Matchmaker Lobby API

## Overview
This API allows clients to create, join, spectate, and manage game lobbies. It also provides WebSocket communication for real-time interaction in lobbies.


 ## Endpoints

### 1. Create a Lobby
- **URL:** `/lobby/create`
- **Method:** `POST`
- **Description:** Creates a new lobby with the specified creator and settings.
- **Request Body:**
  ```json
  {
    "Creator": {
      "TelegramId": long,
      "Name": "string"
    },
    "LobbyName": "string",
    "Password": "string"
  }

-   **Response:**
    -   `200 OK` --- Returns the created lobby details.

### 2\. Join a Lobby as a Player

-   **URL:** `/lobby/{lobbyId:long}/join`
-   **Method:** `POST`
-   **Description:** Joins the specified lobby as a player.
-   **Request Body:**

    ```json
    {
      "Player": {
        "TelegramId": long,
        "Name": "string"
      },
      "Password": "string"
    }

-   **Response:**
    -   `200 OK` --- Returns the updated lobby details.
    -   `404 Not Found` --- If the lobby is not found.
    -   `400 Bad Request` --- If the password is incorrect or the lobby is full.

### 3\. Join a Lobby as a Spectator

-   **URL:** `/lobby/{lobbyId:long}/spectate`
-   **Method:** `POST`
-   **Description:** Joins the specified lobby as a spectator.
-   **Request Body:**
     ```json
    {
      "Player": {
        "TelegramId": long,
        "Name": "string"
      },
      "Password": "string"
    }

-   **Response:**
    -   `200 OK` --- Returns the updated lobby details.
    -   `404 Not Found` --- If the lobby is not found.
    -   `400 Bad Request` --- If the password is incorrect.

### 4\. Start a Lobby

-   **URL:** `/lobby/{lobbyId:long}/start`
-   **Method:** `POST`
-   **Description:** Starts the game in the lobby if there are exactly 2 players.
-   **Response:**
    -   `200 OK` --- If the game is successfully started.
    -   `400 Bad Request` --- If the lobby does not have exactly 2 players.

### 5\. Leave a Lobby

-   **URL:** `/lobby/leave`
-   **Method:** `POST`
-   **Description:** Removes a player or spectator from the lobby.
-   **Request Body:**

      ```json
    {
      "LobbyId": long,
      "PlayerId": long
    }

-   **Response:**
    -   `200 OK` --- If the player successfully leaves the lobby.
    -   `404 Not Found` --- If the lobby or player is not found.
    -   `400 Bad Request` --- If the player is not part of the lobby.

### 6\. List All Lobbies

-   **URL:** `/lobby`
-   **Method:** `POST`
-   **Description:** Returns a list of lobbies, optionally filtered by lobby name.
-   **Request Parameters:**
    -   `filter` (optional): String to filter lobbies by name.
-   **Response:**
    -   `200 OK` --- Returns a list of lobbies.

### 7\. WebSocket Connection for a Lobby

-   **URL:** `/lobby/ws/{lobbyId:long}&{playerId:long}`
-   **Method:** `GET`
-   **Description:** Establishes a WebSocket connection for real-time communication in the lobby.
-   **Path Parameters:**
    -   `lobbyId`: The ID of the lobby.
    -   `playerId`: The ID of the player.
-   **Response:**
    -   `101 Switching Protocols` --- If WebSocket connection is successfully established.
    -   `404 Not Found` --- If the lobby is not found.
    -   `400 Bad Request` --- If the request is not a WebSocket request.

Error Codes
-----------

-   `200 OK`: The request was successful.
-   `400 Bad Request`: There was a problem with the request, such as invalid parameters.
-   `404 Not Found`: The requested resource was not found, such as a missing lobby or player.
-   `500 Internal Server Error`: The server encountered an error processing the request.

WebSocket Messaging
-------------------

When a player joins, leaves, or any event happens in the lobby, a message is sent to all WebSocket clients in the lobby.

How to Use WebSockets
---------------------

1.  Establish a WebSocket connection to `/lobby/ws/{lobbyId}&{playerId}`.
2.  Send and receive messages in real-time during the lobby session.
