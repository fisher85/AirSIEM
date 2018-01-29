<!DOCTYPE html>
<html>
  <head>
    <meta charset="utf-8">
    <title>Buggy Webapp</title>
    <meta name="viewport" content="width=device-width, height=device-height, initial-scale=1.0" />
    <link rel="stylesheet" href="https://maxcdn.bootstrapcdn.com/bootstrap/4.0.0-beta/css/bootstrap.min.css">
    <script src="https://code.jquery.com/jquery-3.2.1.slim.min.js"></script>
    <script src="https://cdnjs.cloudflare.com/ajax/libs/popper.js/1.12.3/umd/popper.min.js"></script>
    <script src="https://maxcdn.bootstrapcdn.com/bootstrap/4.0.0-beta.2/js/bootstrap.min.js"></script>  
  </head>
  <body>
  
  <?php
  if (isset($_GET['logged'])) {
    $logged = $_GET['logged'];
  } else {
    $logged = 0;
  }
  
  $error = 0;
  if (isset($_GET['username']) && isset($_GET['password'])) {
    if (strcmp($_GET['username'], 'admin') == 0 && strcmp($_GET['password'], 'admin') == 0) {
      // Successful login
      header('Location: admin.php?logged=1');
      die();
    } else {
      $error = 1; // Incorrect username or password
    }
  }

  if ($logged != 1): // Unexpected or missed $logged value => show login form 
  ?>
  
    <div class="container py-5">
      <div class="row">
        <div class="col-md-6 mx-auto">
        
        <?php if ($error == 1): ?>
          <div class="alert alert-danger alert-dismissible fade show" role="alert">
            <strong>ERROR!</strong> Incorrect username or password.
            <button type="button" class="close" data-dismiss="alert" aria-label="Close">
              <span aria-hidden="true">&times;</span>
            </button>
          </div>      
        <?php endif; ?>
        
          <div class="card rounded-0">
            <h3 class="card-header">Log In</h3>
            <div class="card-body">
              <form role="form" action="admin.php" method="get">
                <div class="form-group">
                  <label for="username">Username</label>
                  <input type="text" class="form-control rounded-0" name="username" id="username" placeholder="Username" required>
                </div>
                <div class="form-group">
                  <label for="password">Password</label>
                  <input type="password" class="form-control rounded-0" name="password" id="password" placeholder="Password" required>
                </div>
                <button type="submit" class="btn btn-success rounded-0">Log In</button>
              </form>
            </div>
          </div>
        </div>
      </div>
    </div>    
  
  <?php else: // Successful login ?>

    <div class="container py-5">
      <div class="row">
        <div class="col-md-6 mx-auto">
          <div class="card rounded-0">
            <h3 class="card-header">Dashboard</h3>
            <div class="card-body">
              <p class="card-text">Howdy, <strong>admin</strong>!</p>
              <p class="card-text"><a href="admin.php">Log Out</a></p>
            </div>
          </div>
        </div>
      </div>
    </div>
  
  <?php endif; ?>
    
  </body>
</html>