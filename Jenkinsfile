pipeline {
    agent any

    environment {
        VPS_HOST = '217.216.72.181'
        VPS_USER = 'root'
        DEPLOY_DIR = '/opt/ChatBridgeService'
    }

    stages {
        stage('Deploy') {
            steps {
                sshagent(credentials: ['vps-chatbridge']) {
                    sh """
                        ssh -o StrictHostKeyChecking=no ${VPS_USER}@${VPS_HOST} '
                            cd ${DEPLOY_DIR}
                            git pull origin main
                            docker compose up -d --build
                            docker image prune -f
                        '
                    """
                }
            }
        }
    }

    post {
        success {
            echo "ChatBridgeService deployed successfully"
        }
        failure {
            echo "Deployment failed"
        }
    }
}
